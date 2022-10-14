using Simplic.OxS.Data;
using System.Collections;
using System.Text.Json;

namespace Simplic.OxS.Server
{
    /// <summary>
    /// Helper to create patches from http patch requests.
    /// </summary>
    public static class PatchHelper
    {
        /// <summary>
        /// Method to patch the properties of the original document to the values of the patch based on the json when 
        /// the validation request returns true.
        /// </summary>
        /// <typeparam name="T">The type of the documents.</typeparam>
        /// <param name="originalDocument">The original document, loaded from a data source.</param>
        /// <param name="patch">The patch values, mapped from the request object.</param>
        /// <param name="json">The json string which describes the properties that should be patched. 
        /// Should directly taken from the request.</param>
        /// <param name="validation">A func to validate the values before they are set.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="BadRequestException"></exception>
        public static T Patch<T>(T originalDocument, T patch, string json, Func<ValidationRequest, bool> validation)
        {
            if (originalDocument == null)
                throw new ArgumentNullException(nameof(originalDocument));

            if (patch == null)
                throw new ArgumentNullException(nameof(patch));

            if (json == null)
                throw new ArgumentNullException(nameof(json));

            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentOutOfRangeException(nameof(json), "Could not patch with empty request json.");

            try
            {
                using var document = JsonDocument.Parse(json);
                return HandleDocument(originalDocument, patch, document.RootElement, validation);
            }
            catch (JsonException)
            {
                throw new ArgumentException("Json string is no valid Json", nameof(json));
            }
        }

        private static T HandleDocument<T>(T originalDocument, T patch, JsonElement doc,
            Func<ValidationRequest, bool> validationRequest)
        {
            if (originalDocument == null)
                throw new ArgumentNullException(nameof(originalDocument));

            if (patch == null)
                throw new ArgumentNullException(nameof(patch));

            var queue = new Queue<(string ParentPath, JsonElement element)>();
            queue.Enqueue(("", doc));

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
                            queue.Enqueue(($"{parentPath}{nextEl.Name}", nextEl.Value));
                        }
                        break;

                    case JsonValueKind.Array:
                        HandleArray(element, GetCollection(originalDocument, parentPath),
                                    GetCollection(patch, parentPath), parentPath, validationRequest);
                        break;

                    case JsonValueKind.Undefined:
                    case JsonValueKind.String:
                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                    case JsonValueKind.Null:
                        SetSourceValueAtPath(patch, originalDocument, parentPath, validationRequest);
                        break;
                }
            }
            return originalDocument;
        }

        private static void HandleArray(JsonElement element, IList originalCollection, IList patchCollection,
            string path, Func<ValidationRequest, bool> validationRequest)
        {
            var elements = element.EnumerateArray().ToList();
            if (!elements.Any())
                return;

            var firstElement = elements.First();

            switch (firstElement.ValueKind)
            {
                case JsonValueKind.Object:
                    HandleObjectArray(element, originalCollection, patchCollection, validationRequest);
                    break;

                case JsonValueKind.Array:
                    SetSourceValueAtPath(patchCollection, originalCollection, path, validationRequest);
                    break;

                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    SetSourceValueAtPath(patchCollection, originalCollection, path, validationRequest);
                    break;
            }
        }

        private static void HandleObjectArray(JsonElement element, IList originalCollection, IList patchCollection,
            Func<ValidationRequest, bool> validationRequest)
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
                    originalCollection.Add(patchCollection[i]);
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
                    originalCollection.Remove(originalCollection.OfType<IItemId>().First(x => x.Id == idGuid));
                    continue;
                }

                if (idGuid == Guid.Empty)
                {
                    originalCollection.Add(patchCollection[i]);
                    continue;
                }

                var originalItem = originalCollection.OfType<IItemId>().FirstOrDefault(x => x.Id == idGuid);
                if (originalItem == null)
                    throw new BadRequestException($"Could not find item with id {idGuid}");

                var patchItem = patchCollection.OfType<IItemId>().FirstOrDefault(x => x.Id == idGuid);

                HandleDocument(originalItem, patchItem, item, validationRequest);
            }
        }

        private static void SetSourceValueAtPath(object source, object target, string path,
            Func<ValidationRequest, bool> validationRequest)
        {
            Type currentType = source.GetType();
            var splitPath = path.Split(".");

            for (int i = 0; i < splitPath.Length; i++)
            {
                var propertyName = splitPath[i];
                var property = currentType.GetProperty(propertyName);

                if (property == null)
                    throw new BadRequestException($"{currentType.Name} does not contain property: {propertyName}");

                currentType = property.PropertyType;
                var res = property.GetValue(source, null);
                if (res == null)
                    throw new NullReferenceException($"{currentType.Name}.{propertyName} not initialized.");

                source = res;

                if (i == splitPath.Length - 1)
                {
                    var valid = validationRequest.Invoke(new ValidationRequest
                    {
                        Path = path,
                        Property = propertyName,
                        Value = source
                    });

                    if (!valid)
                        throw new BadRequestException($"Validation on {path} failed with value {source}");

                    property.SetValue(target, Convert.ChangeType(source, property.PropertyType));
                }
                else
                {
                    var targetRes = property.GetValue(target, null);
                    if (targetRes == null)
                        throw new NullReferenceException($"{currentType.Name}.{propertyName} not initialized.");

                    target = targetRes;
                }
            }
        }

        private static IList GetCollection(object obj, string path)
        {
            Type currentType = obj.GetType();

            foreach (var propertyName in path.Split("."))
            {
                var property = currentType.GetProperty(propertyName);

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
    }
}

