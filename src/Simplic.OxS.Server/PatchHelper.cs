using Simplic.OxS.Data;
using System;
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

            var queue = new Queue<(string ParentPath, JsonElement element)>();
            queue.Enqueue(("", document.RootElement));

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

                    //case JsonValueKind.Array:
                    //    foreach (var (nextEl, i) in element.EnumerateArray().Select((jsonElement, i) => (jsonElement, i)))
                    //    {
                    //        queue.Enqueue(($"{parentPath}[{i}]", nextEl));
                    //    }
                    //    break;

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

        public static IEnumerable<string> EnumeratePaths(JsonElement doc)
        {
            var queu = new Queue<(string ParentPath, JsonElement element)>();
            queu.Enqueue(("", doc));
            while (queu.Any())
            {
                var (parentPath, element) = queu.Dequeue();
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        parentPath = parentPath == ""
                            ? parentPath
                            : parentPath + ".";
                        foreach (var nextEl in element.EnumerateObject())
                        {
                            queu.Enqueue(($"{parentPath}{nextEl.Name}", nextEl.Value));
                        }
                        break;
                    case JsonValueKind.Array:
                        foreach (var (nextEl, i) in element.EnumerateArray().Select((jsonElement, i) => (jsonElement, i)))
                        {
                            queu.Enqueue(($"{parentPath}[{i}]", nextEl));
                        }
                        break;
                    case JsonValueKind.Undefined:
                    case JsonValueKind.String:
                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                    case JsonValueKind.Null:
                        yield return parentPath;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

        }

        private static void SetValueAtPath(object source, object target, string path)
        {
            Type currentType = source.GetType();
            var splitPath = path.Split(".");

            for (int i = 0; i < splitPath.Length; i++)
            {
                var propertyName = splitPath[i];
                var property = currentType.GetProperty(propertyName);
                source = property.GetValue(source);

                if (i == splitPath.Length - 1)
                    property.SetValue(target, Convert.ChangeType(source, property.PropertyType));

                else
                    target = property.GetValue(target);
            }
        }
    }
}

