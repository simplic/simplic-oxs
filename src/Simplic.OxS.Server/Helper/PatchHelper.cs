﻿using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Data;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Simplic.OxS.Server
{
    /// <summary>
    /// Helper to create patches from http patch requests.
    /// </summary>
    public class PatchHelper
    {
        /// <summary>
        /// Initializes a new instance of the patch helper without any configurations.
        /// </summary>
        public PatchHelper()
        {
            Configuration = new PatchConfiguration();
        }

        /// <summary>
        /// Initializes a new instance of the patch helper with the given configuration.
        /// </summary>
        /// <param name="configuration"></param>
        public PatchHelper(PatchConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Initializes a new instance of the patch helper and allows to directly add configurations in the constructor.
        /// </summary>
        /// <param name="func"></param>
        public PatchHelper(Func<PatchConfiguration, PatchConfiguration> func)
        {
            Configuration = func(new PatchConfiguration());
        }

        /// <summary>
        /// Method to patch the properties of the original document to the values of the patch based on the json when 
        /// the validation request returns true.
        /// </summary>
        /// <typeparam name="T">The type of the documents.</typeparam>
        /// <param name="originalDocument">The original document, loaded from a data source.</param>
        /// <param name="patch">The patch values, mapped from the request object.</param>
        /// <param name="json">The json string which describes the properties that should be patched. 
        /// Should directly taken from the request.</param>
        /// <param name="validation">A func to validate the values before they are set. </param>
        /// <returns>The original document with the patch applied.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="BadRequestException"></exception>
        public async Task<T> Patch<T>(T originalDocument, object patch, string json, Func<ValidationRequest, bool> validation)
        {
            if (originalDocument == null)
                throw new ArgumentNullException(nameof(originalDocument));

            if (patch == null)
                throw new ArgumentNullException(nameof(patch));

            if (json == null)
                throw new ArgumentNullException(nameof(json));

            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentOutOfRangeException(nameof(json), "Could not patch with empty request json.");

            // Validate all if no validation is required.
            if (validation == null)
                validation = (v) => true;

            try
            {
                using var document = JsonDocument.Parse(json);
                return await HandleDocument(originalDocument, patch, document.RootElement, validation, "");
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Json string is no valid Json", nameof(json), ex);
            }
        }

        /// <summary>
        /// Method to handle the document. Moved out of the patch method to be able to call it for objects in an array.
        /// </summary>
        /// <typeparam name="T">Type of the documents and the return type.</typeparam>
        /// <param name="originalDocument">The original document.</param>
        /// <param name="patch">The document containing the patch values.</param>
        /// <param name="jsonDocument">The json document as json element. Should be the root element of the current context.</param>
        /// <param name="validationRequest">The validation reques func from the patch method.</param>
        /// <param name="startingPath">The path the handle document is started with, used since collections sometimes
        /// just use their relative path in the collection and need to append the starting path to get a full path to
        /// the patched property.</param>
        /// <returns>The original document with the patch applied.</returns>
        private async Task<T> HandleDocument<T>(T originalDocument, object patch, JsonElement jsonDocument,
            Func<ValidationRequest, bool> validationRequest, string startingPath)
        {
            if (originalDocument == null)
                throw new ArgumentNullException(nameof(originalDocument));

            if (patch == null)
                throw new ArgumentNullException(nameof(patch));

            var queue = new Queue<(string parentPath, JsonElement element)>();
            queue.Enqueue(("", jsonDocument));

            while (queue.Any())
            {
                var (parentPath, element) = queue.Dequeue();
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        //TODO: Also add dictioanary handling.
                        parentPath = parentPath == ""
                            ? parentPath
                            : parentPath + ".";
                        foreach (var nextEl in element.EnumerateObject())
                        {
                            // Enqueue inner proeprties with their full path.
                            queue.Enqueue(($"{parentPath}{nextEl.Name}", nextEl.Value));
                        }
                        break;

                    case JsonValueKind.Array:
                        var arrayfullPath = parentPath;
                        if (startingPath != string.Empty)
                            arrayfullPath = startingPath + "." + parentPath;
                        await HandleArray(element, originalDocument, patch, parentPath, validationRequest, arrayfullPath);
                        break;

                    // Sets the value for all types that are not array or object.
                    case JsonValueKind.Undefined:
                    case JsonValueKind.String:
                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                    case JsonValueKind.Null:
                        var fullPatch = parentPath;
                        if (startingPath != string.Empty)
                            fullPatch = startingPath + "." + parentPath;

                        await SetSourceValueAtPath(patch, originalDocument, parentPath, validationRequest, fullPatch);
                        break;
                }
            }

            return originalDocument;
        }

        /// <summary>
        /// Handles a json array to patch the original collection.
        /// </summary>
        /// <param name="element">The array as json element.</param>
        /// <param name="originalCollection">The collection of the original document.</param>
        /// <param name="patchCollection">The collection of the patch document.</param>
        /// <param name="path">The path to the array.</param>
        /// <param name="validationRequest">The validation request from the patch.</param>
        /// <param name="fullPath">The full path to the array.</param>
        private async Task HandleArray(JsonElement element, object original, object patch,
             string path, Func<ValidationRequest, bool> validationRequest, string fullPath)
        {
            var elements = element.EnumerateArray().ToList();

            if (!elements.Any())
            {
                var collection = GetCollection(patch, path);
                var type = collection.GetType();

                // To ensure that a list of strings, guids or other value types is set to an empty list when a empty list
                // is send to the patch helper.
                if (type.IsGenericType)
                {
                    var collectionType = type.GetGenericArguments()[0];
                    if (collectionType.IsValueType || collectionType == typeof(string) || !collectionType.GetInterfaces().Any(x => x == typeof(IItemId)))
                    {
                        var originalCollection = GetCollection(original, path);
                        originalCollection.Clear();
                    }
                }

                return;
            }

            var firstElement = elements.First();

            switch (firstElement.ValueKind)
            {
                case JsonValueKind.Object:
                    if (Configuration.CollectionItems.Any(x =>
                        (x.Path.ToLower() == fullPath.ToLower()
                        || (fullPath.ToLower().StartsWith(x.Path.ToLower()) && fullPath.ToLower().EndsWith(x.EndPath.ToLower())))
                        && x.OverwriteCollection))
                    {
                        await SetSourceValueAtPath(patch, original, path, validationRequest, fullPath);
                        return;
                    }

                    await HandleObjectArray(element, GetCollection(original, path), GetCollection(patch, path),
                        validationRequest, fullPath);
                    break;

                case JsonValueKind.Array:
                    //TODO: This might be a bad idea, since the array might contain objects .
                    await SetSourceValueAtPath(GetCollection(original, path), GetCollection(patch, path), path, validationRequest, fullPath);
                    break;

                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (Configuration.CollectionItems.Any(x =>
                        (x.Path.ToLower() == fullPath.ToLower()
                        || (fullPath.ToLower().StartsWith(x.Path.ToLower()) && fullPath.ToLower().EndsWith(x.EndPath.ToLower())))
                        && x.OverwriteCollection))
                    {
                        await SetSourceValueAtPath(patch, original, path, validationRequest, fullPath);
                    }

                    // TODO: This should be tested, but from my current understanding it won't work at the current state. 
                    await SetSourceValueAtPath(patch, original, path, validationRequest, fullPath);
                    break;
            }
        }

        /// <summary>
        /// Handles a array of objects. Each object will call the HandleDocument method again.
        /// </summary>
        /// <param name="element">The array of objects.</param>
        /// <param name="originalCollection">The collection from the original document.</param>
        /// <param name="patchCollection">The collection from the patch document.</param>
        /// <param name="validationRequest">The validation request from the patch method.</param>
        private async Task HandleObjectArray(JsonElement element, IList originalCollection, IList patchCollection,
            Func<ValidationRequest, bool> validationRequest, string path)
        {
            if (element.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Element is no array");

            foreach (var (item, i) in element.EnumerateArray().Select((item, i) => (item, i)))
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("Element is not an array of objects");

                var elements = item.EnumerateObject().ToList();

                if (!elements.Any(x => x.Name.ToLower() == "id"))
                {
                    await AddNewItemToCollection(originalCollection, patchCollection[i], item, validationRequest, path);
                    continue;
                }

                var idProperty = elements.First(x => x.Name.ToLower() == "id");
                var idElement = idProperty.Value;

                var idString = idElement.GetString();
                var isGuid = Guid.TryParse(idString, out var idGuid);

                if (!isGuid)
                    throw new BadRequestException($"Id of item {idString} cannot be parsed to 'Guid'");

                if (elements.Any(x => x.Name.ToLower() == "_remove" && x.Value.GetBoolean()))
                {
                    if (!validationRequest(new ValidationRequest
                    {
                        Path = path,
                        Property = path.Split(".").Last(),
                        Type = ValidationRequestType.RemoveItem,
                        OriginalItem = originalCollection.OfType<IItemId>().First(x => x.Id == idGuid),
                        PatchItem = originalCollection.OfType<IItemId>().First(x => x.Id == idGuid)
                    }))
                    {
                        throw new BadRequestException($"Removing of item to {path} is forbidden in the current state.");
                    }

                    originalCollection.Remove(originalCollection.OfType<IItemId>().First(x => x.Id == idGuid));
                    continue;
                }

                if (idGuid == Guid.Empty)
                {
                    await AddNewItemToCollection(originalCollection, patchCollection[i], item, validationRequest, path);
                    continue;
                }

                var originalItem = originalCollection.OfType<IItemId>().FirstOrDefault(x => x.Id == idGuid);
                if (originalItem == null)
                    throw new BadRequestException($"Could not find item with id {idGuid}." +
                        "A reason might be that items of the collection does not derive from IITemId");

                var patchItem = patchCollection.OfType<IItemId>().FirstOrDefault(x => x.Id == idGuid);

                await HandleDocument(originalItem, patchItem, item, validationRequest, path);
            }
        }

        /// <summary>
        /// Adds a new item to the original collection.
        /// </summary>
        /// <param name="originalCollection"></param>
        /// <param name="patchItem"></param>
        /// <param name="jsonElement"></param>
        /// <param name="validationRequest"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private async Task AddNewItemToCollection(IList originalCollection, object patchItem, JsonElement jsonElement,
            Func<ValidationRequest, bool> validationRequest, string path)
        {
            var configItem = Configuration.CollectionItems.FirstOrDefault(x =>
                        x.Path.ToLower() == path.ToLower()
                        || (path.ToLower().StartsWith(x.Path.ToLower()) && path.ToLower().EndsWith(x.EndPath.ToLower())));

            //Func to create an instance of the generic type of the collection. 
            var func = new Func<object>(() =>
            {
                var itemType = originalCollection.GetType().GetGenericArguments()[0];
                return Activator.CreateInstance(itemType);
            });

            // Will call the GetNewItem method if the config item is not null, and create a new instance with the 
            // Activator otherwise.
            var obj = configItem != null ? configItem.GetNewItem(patchItem) : func();

            await HandleDocument(obj, patchItem, jsonElement, validationRequest, path);

            //here boh the patch and the original item are set to the obj, since both make sense in a way.
            if (!validationRequest(new ValidationRequest
            {
                Path = path,
                Property = path.Split(".").Last(),
                Type = ValidationRequestType.AddItem,
                PatchItem = patchItem,
                OriginalItem = obj
            }))
            {
                throw new BadRequestException($"Adding of item to {path} is forbidden in the current state.");
            }

            originalCollection.Add(obj);
        }

        private async Task SetSourceValueAtPath(object patch, object original, string path,
            Func<ValidationRequest, bool> validationRequest, string fullPath)
        {
            if (path.EndsWith("_remove"))
                return;

            var configItem = Configuration.Items.FirstOrDefault(x => x.Path.ToLower() == fullPath.ToLower() ||
                (fullPath.ToLower().StartsWith(x.Path.ToLower()) && fullPath.ToLower().EndsWith(x.EndPath.ToLower())));

            PropertyInfo? patchProperty = null;
            PropertyInfo? originalProperty = null;

            try
            {
                if (configItem != null)
                {
                    if (!validationRequest.Invoke(new ValidationRequest
                    {
                        Path = fullPath,
                        Property = fullPath.Split(".").Last(),
                        Type = ValidationRequestType.UpdateProperty,
                        OriginalItem = original,
                        PatchItem = patch
                    }))
                        throw new BadRequestException($"Validation on {path} failed with value {patch}");

                    await configItem.ApplyChange(original, patch);
                    return;
                }

                Type currentPatchType = patch.GetType();
                Type currentOriginalType = original.GetType();
                var splitPath = path.Split(".");
                object originalParent = null;
                object patchParent = null;

                for (int i = 0; i < splitPath.Length; i++)
                {
                    var propertyName = splitPath[i];

                    if (propertyName.ToLower() == "id")
                        return;

                    if (currentOriginalType.IsGenericType && currentOriginalType.GetGenericTypeDefinition()
                        == typeof(Dictionary<,>))
                    {
                        Type[] types = currentOriginalType.GetGenericArguments();
                        Type keyType = types[0];
                        Type valueType = types[1];
                        Type genericType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);

                        var dict = patchProperty.GetValue(patchParent, null) as IDictionary;
                        var value = dict[propertyName];

                        dict = originalProperty.GetValue(originalParent, null) as IDictionary;
                        dict[propertyName] = value;

                        return;
                    }

                    patchProperty = currentPatchType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    originalProperty = currentOriginalType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                    if (patchProperty == null)
                        throw new BadRequestException($"{currentPatchType.Name} does not contain property: {propertyName}");

                    if (originalProperty == null)
                        throw new BadRequestException($"{currentOriginalType.Name} does not contain property: {propertyName}");

                    currentPatchType = patchProperty.PropertyType;
                    var res = patchProperty.GetValue(patch, null);
                    if (res == null && i != splitPath.Length - 1)
                        throw new NullReferenceException($"{currentPatchType.Name}.{propertyName} not initialized.");


                    if (i == splitPath.Length - 1)
                    {
                        if (!validationRequest.Invoke(new ValidationRequest
                        {
                            Path = fullPath,
                            Property = propertyName,
                            Value = res,
                            Type = ValidationRequestType.UpdateProperty,
                            PatchItem = patch,
                            OriginalItem = original
                        }))
                            throw new BadRequestException($"Validation on {path} failed with value {patch}");

                        patch = res;

                        try
                        {
                            var collectionConfigItem = Configuration.CollectionItems.FirstOrDefault(x =>
                                    x.OverwriteCollection &&
                                    (
                                        x.Path.ToLower() == fullPath.ToLower()
                                        || (
                                                !string.IsNullOrWhiteSpace(x.EndPath)
                                                && fullPath.ToLower().StartsWith(x.Path.ToLower())
                                                && fullPath.ToLower().EndsWith(x.EndPath.ToLower())
                                           )
                                    )
                                 );

                            if (collectionConfigItem != null)
                            {
                                originalProperty.SetValue(original, collectionConfigItem.GetAsOriginalType(patch));
                                return;
                            }

                            originalProperty.SetValue(original, patch);

                        }
                        catch (InvalidCastException ex)
                        {
                            if (originalProperty.PropertyType.IsGenericType)
                            {
                                if (patch == null)
                                {
                                    originalProperty.SetValue(original, null);
                                    return;
                                }

                                var type = Nullable.GetUnderlyingType(originalProperty.PropertyType);

                                if (type != null)
                                    originalProperty.SetValue(original, Convert.ChangeType(patch, type));
                                else
                                {
                                    throw;
                                }
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                    else
                    {
                        var originalValue = originalProperty.GetValue(original, null);
                        if (originalValue == null)
                        {
                            try
                            {
                                var type = originalProperty.PropertyType;
                                var value = type.GetConstructor(new Type[] { })?.Invoke(new object[] { });
                                originalProperty.SetValue(original, value);
                                originalValue = originalProperty.GetValue(original, null);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception($"Could not initialize not initialized type {currentOriginalType.Name}.{propertyName}");
                            }
                        }

                        patchParent = patch;
                        patch = res;
                        originalParent = original;
                        original = originalValue;
                        currentOriginalType = original.GetType();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is BadRequestException)
                    throw ex;

                var builder = new StringBuilder();
                builder.AppendLine($"Path: {fullPath}");
                builder.AppendLine($"Value to set: {patch?.ToString() ?? "<null>"}  of type {path?.GetType().FullName}");

                if (patchProperty != null)
                    builder.AppendLine($"Patch property: {patchProperty.Name} / {patchProperty.PropertyType.FullName}");

                if (originalProperty != null)
                    builder.AppendLine($"Original property: {originalProperty.Name} / {originalProperty.PropertyType.FullName}");

                throw new SetValueException(builder.ToString(), ex);
            }
        }

        private static IList GetCollection(object obj, string path)
        {
            Type currentType = obj.GetType();

            foreach (var propertyName in path.Split("."))
            {
                var property = currentType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                    throw new ArgumentException($"{currentType.Name} does not contain property: {propertyName}");

                var res = property.GetValue(obj, null);
                if (res == null)
                    throw new NullReferenceException($"{currentType.Name}.{propertyName} not initialized.");

                obj = res;
                currentType = property.PropertyType;
            }

            if (obj is IList collection)
                return collection;

            throw new ArgumentException($"Collection at {path} does not derive from IList");
        }

        public PatchConfiguration Configuration { get; set; }
    }
}

