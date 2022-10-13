using Simplic.OxS.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Simplic.OxS.Server
{
    public static class PatchHelper
    {
        public static T CreatePatch<T, I>(T originalDocument, T patch, string json, Func<ValidationRequest, bool> validation) where T : IDocument<I>
        {
            using var document = JsonDocument.Parse(json);
            return HandleDocument<T>(originalDocument, patch, document.RootElement);

        }

        private static void HandleArray(JsonElement element, IEnumerable originalCollection, IEnumerable patchCollection, string path)
        {
            var elements = element.EnumerateArray().ToList();
            if (!elements.Any())
                return;

            var firstElement = elements.First();

            switch (firstElement.ValueKind)
            {
                case JsonValueKind.Object:
                    HandleObjectArray(element, originalCollection.OfType<IItemId>(), patchCollection.OfType<IItemId>());
                    break;

                case JsonValueKind.Array:
                    SetValueAtPath(patchCollection, originalCollection, path);
                    break;
            }
        }

        private static void HandleObjectArray(JsonElement element, IEnumerable<IItemId> originalCollection, IEnumerable<IItemId> patchCollection)
        {
            if (element.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Element is no array");

            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("Element is not an array of objects");


                var elements = item.EnumerateObject().ToList();
                if (!elements.Any(x => x.Name.ToLower() == "id"))
                    //new Item
                    break;

                var idProperty = elements.First(x => x.Name.ToLower() == "id");
                var idElement = idProperty.Value;


                var idString = idElement.GetString();
                var isGuid = Guid.TryParse(idString, out var idGuid);

                if (!isGuid)
                    throw new ArgumentException();

                if (idGuid == Guid.Empty)
                {
                    //newItem
                    break;
                }

                var originalItem = originalCollection.FirstOrDefault(x => x.Id == idGuid);
                if (originalItem == null)
                    throw new Exception();

                var patchItem = patchCollection.FirstOrDefault(x => x.Id == idGuid);

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
                        SetValueAtPath(patch, originalDocument, parentPath);
                        break;

                }
            }

            return originalDocument;
        }

        private static void SetValueAtPath(object source, object target, string path)
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

        private static IEnumerable GetCollection(object obj, string path)
        {
            Type currentType = obj.GetType();

            foreach (var propertyName in path.Split("."))
            {
                var property = currentType.GetProperty(propertyName);
                obj = property.GetValue(obj);
                currentType = property.PropertyType;
            }

            if (obj is IEnumerable collection)
                return collection;

            throw new ArgumentException();
        }
    }
}

