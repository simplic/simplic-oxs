using Simplic.OxS.ModelDefinition.Extenstion.Abstractions;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System;

namespace Simplic.OxS.ModelDefinition.Service
{
    public static class ModelDefinitionService
    {
        public static ModelDefinition GenerateDefinitionForController(Type controller)
        {
            var modelDefinition = new ModelDefinition();

            var methods = controller.GetMethods();

            var getMethod = GetOperationFromAttribute(methods, typeof(ModelDefinitionGetOperationAttribute));
            var postMethod = GetOperationFromAttribute(methods, typeof(ModelDefinitionPostOperationAttribute));
            var patchMethod = GetOperationFromAttribute(methods, typeof(ModelDefinitionPatchOperationAttribute));
            var putMethod = GetOperationFromAttribute(methods, typeof(ModelDefinitionPutOperationAttribute));
            var deleteMethod = GetOperationFromAttribute(methods, typeof(ModelDefinitionDeleteOperationAttribute));

            if (getMethod is not null)
                BuildGet(modelDefinition, getMethod);

            if (postMethod is not null)
                BuildPost(modelDefinition, postMethod);

            if (patchMethod is not null)
                BuildPatch(modelDefinition, patchMethod);

            if (putMethod is not null)
                BuildPut(modelDefinition, putMethod);

            if (deleteMethod is not null)
                BuildDelete(modelDefinition, deleteMethod);

            return modelDefinition;
        }

        #region Operations

        private static MethodInfo? GetOperationFromAttribute(IList<MethodInfo> methods, Type attributeType)
        {
            return methods.FirstOrDefault(method =>
                 method.CustomAttributes.Any(attribute =>
                    attribute.AttributeType == attributeType));
        }

        private static void BuildGet(ModelDefinition modelDefinition, MethodInfo getMethod)
        {
            var attribute = Attribute.GetCustomAttribute(getMethod, typeof(ModelDefinitionGetOperationAttribute));

            if (attribute is not ModelDefinitionGetOperationAttribute getAttribute)
                return;

            modelDefinition.Operations.Get = new OperationDefinition
            {
                Endpoint = getAttribute.Endpoint,
                ResponseReference = GetReferenceName(getAttribute.Response),
                Type = "http-get"
            };

            BuildPropertiesOrReference(modelDefinition, getAttribute.Response);
        }

        private static void BuildPost(ModelDefinition modelDefinition, MethodInfo postMethod)
        {

            var attribute = Attribute.GetCustomAttribute(postMethod, typeof(ModelDefinitionPostOperationAttribute));

            if (attribute is not ModelDefinitionPostOperationAttribute postAttribute)
                return;

            modelDefinition.Operations.Create = new OperationDefinition
            {
                Endpoint = postAttribute.Endpoint,
                RequestReference = GetReferenceName(postAttribute.Request),
                ResponseReference = GetReferenceName(postAttribute.Response),
                Type = "http-post"
            };

            BuildPropertiesOrReference(modelDefinition, postAttribute.Request);
            BuildPropertiesOrReference(modelDefinition, postAttribute.Response);
        }

        private static void BuildPatch(ModelDefinition modelDefinition, MethodInfo patchMethod)
        {
            var attribute = Attribute.GetCustomAttribute(patchMethod, typeof(ModelDefinitionPatchOperationAttribute));

            if (attribute is not ModelDefinitionPatchOperationAttribute patchAttribute)
                return;

            modelDefinition.Operations.Update = new OperationDefinition
            {
                Endpoint = patchAttribute.Endpoint,
                RequestReference = GetReferenceName(patchAttribute.Request),
                ResponseReference = GetReferenceName(patchAttribute.Response),
                Type = "http-patch"
            };

            BuildPropertiesOrReference(modelDefinition, patchAttribute.Request);
            BuildPropertiesOrReference(modelDefinition, patchAttribute.Response);
        }

        private static void BuildPut(ModelDefinition modelDefinition, MethodInfo putMethod)
        {
            var attribute = Attribute.GetCustomAttribute(putMethod, typeof(ModelDefinitionPutOperationAttribute));

            if (attribute is not ModelDefinitionPutOperationAttribute putAttribute)
                return;

            modelDefinition.Operations.Update = new OperationDefinition
            {
                Endpoint = putAttribute.Endpoint,
                RequestReference = GetReferenceName(putAttribute.Request),
                ResponseReference = GetReferenceName(putAttribute.Response),
                Type = "http-put"
            };

            BuildPropertiesOrReference(modelDefinition, putAttribute.Request);
            BuildPropertiesOrReference(modelDefinition, putAttribute.Response);
        }

        private static void BuildDelete(ModelDefinition modelDefinition, MethodInfo deleteMethod)
        {
            var attribute = Attribute.GetCustomAttribute(deleteMethod, typeof(ModelDefinitionDeleteOperationAttribute));

            if (attribute is not ModelDefinitionDeleteOperationAttribute deleteAttribute)
                return;

            modelDefinition.Operations.Delete = new OperationDefinition
            {
                Endpoint = deleteAttribute.Endpoint,
                Type = "http-delete"
            };
        }

        private static void BuildPropertiesOrReference(ModelDefinition modelDefinition, Type response)
        {
            if (modelDefinition.Model != null
                && modelDefinition.Model.Equals(GetReferenceName(response)))
                // Return since the type is alrady the model type.
                return;

            var attribute = Attribute.GetCustomAttribute(response, typeof(DataSourceAttribute));
            if (attribute != null && attribute is DataSourceAttribute dataSourceAttribute)
            {
                var dataSource = new DataSource
                {
                    Endpoint = dataSourceAttribute.Endpoint,
                    GqlEntryPoint = dataSourceAttribute.GqlEntryPoint
                };
                switch (dataSourceAttribute.Type)
                {
                    case Extenstion.Abstractions.DataSourceType.HttpGet:
                        dataSource.Type = DataSourceType.HttpGet;
                        break;

                    case Extenstion.Abstractions.DataSourceType.GraphQL:
                        dataSource.Type = DataSourceType.GraphQL;
                        break;

                    default:
                        break;
                }

                if (!modelDefinition.DataSources.Any(x => x.Endpoint == dataSource.Endpoint &&
                    x.Type == dataSource.Type && x.GqlEntryPoint == dataSource.GqlEntryPoint))
                    modelDefinition.DataSources.Add(dataSource);
            }


            if (modelDefinition.Model == null)
            {
                modelDefinition.Model = GetReferenceName(response);
                modelDefinition.Properties = BuildProperties(response, modelDefinition);
            }
            else
            {
                AddRefereceDefinition(modelDefinition, response);
            }
        }

        #endregion

        #region Properties

        private static IList<PropertyDefinition> BuildProperties(Type model, ModelDefinition modelDefinition)
        {
            var properties = new List<PropertyDefinition>();
            foreach (var property in model.GetProperties())
            {
                var propertyDefinition = new PropertyDefinition
                {
                    Name = ToCamelCase(property.Name),
                    Description = "", //https://learn.microsoft.com/en-us/archive/msdn-magazine/2019/october/csharp-accessing-xml-documentation-via-reflection
                    Nullable = Nullable.GetUnderlyingType(property.PropertyType) != null,
                };

                propertyDefinition.Internal = Attribute.GetCustomAttribute(
                        property,
                        typeof(InternalPropertyAttribute))
                    is not null;

                propertyDefinition.Required = Attribute.GetCustomAttribute(
                        property,
                        typeof(RequiredAttribute))
                    is not null;

                var referenceIdAttribute = Attribute.GetCustomAttribute(
                    property,
                    typeof(ReferenceIdAttribute)) as ReferenceIdAttribute;

                if (referenceIdAttribute is not null)
                    propertyDefinition.ReferenceId = referenceIdAttribute.ReferenceIdPropertyName;

                var availableTypeAttributes = Attribute.GetCustomAttributes(
                    property,
                    typeof(AvailableTypeAttribute));

                if (availableTypeAttributes is not null &&
                    availableTypeAttributes.Length != 0)

                {
                    propertyDefinition.AvailableTypes = new List<string>();
                    foreach (var availableTypeAttribute in availableTypeAttributes.OfType<AvailableTypeAttribute>())
                    {
                        propertyDefinition.AvailableTypes.Add(GetReferenceName(availableTypeAttribute.Type));
                        AddRefereceDefinition(modelDefinition, availableTypeAttribute.Type);
                    }
                }
                if (property.PropertyType.IsSimpleType())
                {
                    (propertyDefinition.MinValue, propertyDefinition.MaxValue) = GetMinAndMaxValue(property);

                    var underlyingType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    propertyDefinition.Type = GetFriendlyTypeName(underlyingType);

                    propertyDefinition.Format = GetFormat(property);
                }
                else if (property.PropertyType.IsCollectionType())
                {
                    Type? arrayType = null;

                    if (property.PropertyType.IsArray)
                        arrayType = property.PropertyType.GetElementType();

                    if (property.PropertyType.IsGenericType)
                        arrayType = property.PropertyType.GetGenericArguments()[0];

                    if (arrayType is null && property.PropertyType.GetInterfaces().Any())
                    {
                        foreach (var interfaceType in property.GetType().GetInterfaces())
                        {
                            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                            {
                                arrayType = interfaceType.GetGenericArguments()[0];
                                break;
                            }
                        }
                    }

                    if (arrayType is not null)
                    {
                        if (arrayType.IsSimpleType())
                        {
                            propertyDefinition.ArrayType = GetFriendlyTypeName(arrayType);
                            propertyDefinition.PatchableArray = false;
                        }
                        else
                        {
                            propertyDefinition.ArrayType = GetReferenceName(arrayType);
                            propertyDefinition.PatchableArray = FindInterfaceByName(arrayType, "IItemId");

                            AddRefereceDefinition(modelDefinition, arrayType);
                        }
                    }
                }
                else if (property.PropertyType.IsEnum)
                {
                    propertyDefinition.Type = property.PropertyType.Name;
                    propertyDefinition.EnumType = GetFriendlyTypeName(Enum.GetUnderlyingType(property.PropertyType));
                    propertyDefinition.EnumItems = GetEnumItems(property.GetType());
                }
                else
                {
                    propertyDefinition.Type = GetReferenceName(property.PropertyType);
                    AddRefereceDefinition(modelDefinition, property.PropertyType);
                }

                properties.Add(propertyDefinition);
            }

            return properties;
        }

        private static (string? min, string? max) GetMinAndMaxValue(PropertyInfo property)
        {
            string? minValue = null;
            string? maxValue = null;

            if (property.PropertyType == typeof(string))
            {
                var stringLenthAttribute = Attribute.GetCustomAttribute(property, typeof(StringLengthAttribute))
                    as StringLengthAttribute;

                if (stringLenthAttribute != null)
                    return (stringLenthAttribute.MinimumLength.ToString(), stringLenthAttribute.MaximumLength.ToString());

                var minLengthAttribute = Attribute.GetCustomAttribute(property, typeof(MinLengthAttribute))
                    as MinLengthAttribute;
                var maxLengthAttribute = Attribute.GetCustomAttribute(property, typeof(MaxLengthAttribute))
                    as MaxLengthAttribute;

                if (minLengthAttribute != null)
                    minValue = minLengthAttribute.Length.ToString();

                if (maxLengthAttribute != null)
                    maxValue = maxLengthAttribute.Length.ToString();

                return (minValue, maxValue);
            }

            var attribute = Attribute.GetCustomAttribute(property, typeof(RangeAttribute));

            if (attribute != null && attribute is RangeAttribute rangeAttribute)
            {
                minValue = rangeAttribute.Minimum?.ToString();
                maxValue = rangeAttribute.Maximum?.ToString();
            }

            return (minValue, maxValue);
        }

        private static IList<EnumItem>? GetEnumItems(Type type)
        {
            if (!type.IsEnum)
                return null;

            var enumItems = new List<EnumItem>();
            foreach (var item in type.GetFields())
            {
                var enumItem = new EnumItem
                {
                    Name = item.Name
                };

                if (long.TryParse(item.GetRawConstantValue()?.ToString(), out var longValue))
                    enumItem.Value = longValue;

                enumItems.Add(enumItem);
            }

            return enumItems;
        }

        private static string? GetFormat(PropertyInfo property)
        {
            if (Attribute.GetCustomAttribute(property, typeof(EmailAddressAttribute)) != null)
                return "email";

            if (Attribute.GetCustomAttribute(property, typeof(FileExtensionsAttribute)) != null)
                return "file-extension";

            if (Attribute.GetCustomAttribute(property, typeof(PhoneAttribute)) != null)
                return "phone-number";

            return null;
        }

        #endregion

        #region References
        private static void AddRefereceDefinition(ModelDefinition modelDefinition, Type type)
        {
            // Reference already present.
            if (modelDefinition.References.Any(x => x.Model.Equals(GetReferenceName(type))))
                return;

            // There should be no need to add the base model to the references even when the base model is referenced 
            // anywhere.
            if (modelDefinition.Model!.Equals(GetReferenceName(type)))
                return;

            var referenceDefinition = new ReferenceDefinition
            {
                Model = GetReferenceName(type),
                Title = type.Name,
                Properties = BuildProperties(type, modelDefinition),
                ReferencePropertyName = GetReferencePropertyName(type),
            };

            var attribute = Attribute.GetCustomAttribute(type, typeof(SourceAttribute));
            if (attribute != null && attribute is SourceAttribute sourceAttribute)
            {
                referenceDefinition.SourceUrl = sourceAttribute.SourceUrl;
                referenceDefinition.Operation = new OperationDefinition
                {
                    Endpoint = sourceAttribute.Endpoint,
                    Type = "http-get"
                };
            }
            else
            {
                referenceDefinition.SourceUrl = modelDefinition.SourceUrl;
            }

            attribute = Attribute.GetCustomAttribute(type, typeof(SearchKeyAttribute));
            if (attribute != null && attribute is SearchKeyAttribute searchKeyAttribute)
            {
                referenceDefinition.SearchKey = searchKeyAttribute.SearchKey;
            }

            modelDefinition.References.Add(referenceDefinition);
        }

        /// <summary>
        ///  Gets the name of the reference property.
        /// </summary>
        private static string? GetReferencePropertyName(Type type)
        {
            PropertyInfo? property = type.GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<ReferencePropertyAttribute>() != null);

            return property?.Name;
        }

        private static bool FindInterfaceByName(Type type, string interfaceName)
        {
            while (type != null)
            {
                if (type.GetInterfaces().Any(i => i.Name == interfaceName))
                    return true;
                type = type.BaseType; // Move up the inheritance chain
            }
            return false;
        }

        private static string GetReferenceName(Type type) => $"${type.Name}";

        private static string ToCamelCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return char.ToLower(input[0]) + input.Substring(1);
        }
        private static string GetFriendlyTypeName(Type type)
        {
            return type switch
            {
                { } t when t == typeof(byte) => "byte",
                { } t when t == typeof(sbyte) => "sbyte",
                { } t when t == typeof(short) => "short",
                { } t when t == typeof(ushort) => "ushort",
                { } t when t == typeof(int) => "int",
                { } t when t == typeof(uint) => "uint",
                { } t when t == typeof(long) => "long",
                { } t when t == typeof(ulong) => "ulong",
                _ => type.Name // Fallback (e.g., "Int32" instead of "int")
            };
        }

        #endregion
    }
}
