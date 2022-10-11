using Simplic.OxS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
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

            // 
            var patchJsonObject = JsonObject.Parse(json);
            var patchObject = patchJsonObject.Deserialize<T>();






            // Filter for object id
            // var filter = Builders<BsonDocument>.Filter.Eq("_id", model.Id);
            // 
            // UpdateDefinition<BsonDocument> update = null;
            // foreach (var change in changesDocument)
            // {
            //     if (update == null)
            //     {
            //         var builder = Builders<BsonDocument>.Update;
            //         update = builder.Set("", change.Value);
            //     }
            //     else
            //     {
            //         update = update.Set("", change.Value);
            //     }
            // }

            // Find values that needs to be patched

            return default;
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




    }
}

