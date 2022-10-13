using Simplic.OxS.Data;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Simplic.OxS.Server
{
    public static class PatchHelper
    {
        public static T CreatePatch<T, I>(T originalDocument, T patch, string json, Func<ValidationRequest, bool> validation) where T : IDocument<I>
        {
            using var document = JsonDocument.Parse(json);
            return HandleDocument<T>(originalDocument, patch, document.RootElement);

        }

        private static void HandleArray(JsonElement element, IList originalCollection, IList patchCollection, string path)
        {
            var elements = element.EnumerateArray().ToList();
            if (!elements.Any())
                return;

            var firstElement = elements.First();

            switch (firstElement.ValueKind)
            {
                case JsonValueKind.Object:
                    HandleObjectArray(element, originalCollection, patchCollection);
                    break;

                case JsonValueKind.Array:
                    SetSourceValueAtPath(patchCollection, originalCollection, path);
                    break;

                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    SetSourceValueAtPath(patchCollection, originalCollection, path);
                    break;
            }
        }

        private static void HandleObjectArray(JsonElement element, IList originalCollection, IList patchCollection)
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
                    throw new ArgumentException();

                if (elements.Any(x => x.Name.ToLower() == "_remove" && x.Value.GetBoolean()))
                {
                    originalCollection.Remove(originalCollection.OfType<IItemId>().First(x => x.Id == idGuid));
                }

                if (idGuid == Guid.Empty)
                {
                    originalCollection.Add(patchCollection[i]);
                    continue;
                }

                var originalItem = originalCollection.OfType<IItemId>().FirstOrDefault(x => x.Id == idGuid);
                if (originalItem == null)
                    throw new Exception();

                var patchItem = patchCollection.OfType<IItemId>().FirstOrDefault(x => x.Id == idGuid);

                HandleDocument(originalItem, patchItem, item);
            }
        }

        private static T HandleDocument<T>(T originalDocument, T patch, JsonElement doc)
        {
            var queue = new Queue<(string ParentPath, JsonElement element)>();
            queue.Enqueue(("", doc));

            while (queue.Any())
            {
                var (parentPath, element) = queue.Dequeue();
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
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
                                    GetCollection(patch, parentPath), parentPath);
                        break;

                    case JsonValueKind.Undefined:
                    case JsonValueKind.String:
                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                    case JsonValueKind.Null:
                        SetSourceValueAtPath(patch, originalDocument, parentPath);
                        break;
                }
            }
            return originalDocument;
        }

        private static void SetSourceValueAtPath(object source, object target, string path)
        {
            Type currentType = source.GetType();
            var splitPath = path.Split(".");
            
            for (int i = 0; i < splitPath.Length; i++)
            {
                var propertyName = splitPath[i];
                var property = currentType.GetProperty(propertyName);
                currentType = property.PropertyType;
                source = property.GetValue(source);

                if (i == splitPath.Length - 1)
                    property.SetValue(target, Convert.ChangeType(source, property.PropertyType));

                else
                    target = property.GetValue(target);
            }
        }

        private static IList GetCollection(object obj, string path)
        {
            Type currentType = obj.GetType();

            foreach (var propertyName in path.Split("."))
            {
                var property = currentType.GetProperty(propertyName);
                obj = property.GetValue(obj);
                currentType = property.PropertyType;
            }

            if (obj is IList collection)
                return collection;

            throw new ArgumentException();
        }
    }
}

