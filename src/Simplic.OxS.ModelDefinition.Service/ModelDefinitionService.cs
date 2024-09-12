﻿using Simplic.OxS.ModelDefinition.Extenstion.Abstractions;
using System.Reflection;
using System.ComponentModel.DataAnnotations;

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
                    Name = property.Name,
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

                    propertyDefinition.Type = Nullable.GetUnderlyingType(property.PropertyType)?.Name ?? property.PropertyType.Name;

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
                            propertyDefinition.ArrayType = arrayType.ToString();
                        }
                        else
                        {
                            propertyDefinition.ArrayType = GetReferenceName(arrayType);
                            AddRefereceDefinition(modelDefinition, arrayType);
                        }
                    }
                }
                else if (property.PropertyType.IsEnum)
                {
                    propertyDefinition.Type = property.PropertyType.Name;
                    propertyDefinition.EnumType = Enum.GetUnderlyingType(property.PropertyType)?.ToString();
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
                Tilte = type.Name,
                Properties = BuildProperties(type, modelDefinition)
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
                referenceDefinition.SeatchKey = searchKeyAttribute.SearchKey;
            }

            modelDefinition.References.Add(referenceDefinition);
        }

        private static string GetReferenceName(Type type) => $"${type.Name}";

        #endregion
    }
}
