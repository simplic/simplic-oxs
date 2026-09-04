using System.Text.Json;
using System.Text.Json.Serialization;
using Simplic.OxS.ModelDefinition.Service;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The legacy <c>/ModelDefinition</c> document, built once at startup and held in memory. Its
    /// derivation is frozen: never re-derive it from the schema, never fold anything into it.
    /// </summary>
    public sealed class ModelDefinitionDocument
    {
        /// <summary>
        /// The legacy serialisation: PascalCase, nulls omitted, indented, CRLF. All four are wire
        /// facts; the newline is pinned so the bytes are the same on every platform.
        /// </summary>
        internal static readonly JsonSerializerOptions Legacy = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            NewLine = "\r\n",
        };

        private ModelDefinitionDocument(byte[] body, IReadOnlyList<ModelDefinition.ModelDefinition> models, IReadOnlyList<string> failures)
        {
            Body = body;
            Models = models;
            Failures = failures;
        }

        /// <summary>The response body, serialised once.</summary>
        public byte[] Body { get; }

        /// <summary>The definitions behind <see cref="Body"/>, so the schema build can read the ids this document publishes.</summary>
        public IReadOnlyList<ModelDefinition.ModelDefinition> Models { get; }

        /// <summary>How many controller definitions the document carries.</summary>
        public int DefinitionCount => Models.Count;

        /// <summary>Controllers whose definition could not be generated, one message each. Logged at startup, never served.</summary>
        public IReadOnlyList<string> Failures { get; }

        /// <summary>Builds the document, or returns null when the host declares no controllers, which the endpoint answers with 404.</summary>
        internal static ModelDefinitionDocument? Build(IReadOnlyList<Type> controllerTypes)
        {
            if (controllerTypes.Count == 0)
                return null;

            var definitions = new List<ModelDefinition.ModelDefinition>(controllerTypes.Count);
            var failures = new List<string>();

            foreach (var controller in controllerTypes)
            {
                // One controller that the legacy generator cannot describe drops that controller
                // only; its exception text must never be served from an anonymous endpoint.
                try
                {
                    definitions.Add(ModelDefinitionService.GenerateDefinitionForController(controller));
                }
                catch (Exception exception)
                {
                    failures.Add($"{controller.FullName}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            return new ModelDefinitionDocument(JsonSerializer.SerializeToUtf8Bytes(definitions, Legacy), definitions, failures);
        }
    }
}
