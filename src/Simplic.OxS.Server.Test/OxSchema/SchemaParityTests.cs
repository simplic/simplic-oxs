using OxQL.Model;
using OxQL.Model.Build;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>
    /// The facts the schema and the query engine each hold a copy of. They live in different
    /// packages and both reach the wire, so a copy that drifts is a format break nobody sees
    /// until a consumer refuses a document.
    /// </summary>
    [Collection(SchemaCollection.Name)]
    public class SchemaParityTests
    {
        [Fact]
        public void Kinds_TheModelCanProduce_AreAllKindsTheDocumentDeclares()
        {
            var declared = typeof(OxSchemaKinds)
                .GetFields()
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!)
                .ToHashSet(System.StringComparer.Ordinal);

            var produced = System.Enum.GetValues<Kind>().Select(Kinds.NameOf).ToList();

            // The walker forwards the engine's vocabulary verbatim. A kind the engine gains and
            // the document does not declare reaches a reader as an unknown kind, and the
            // generator refuses the whole document rather than the one member: the vocabulary is
            // closed within a major version, and `unknown` is the escape hatch.
            produced.Should().OnlyContain(kind => declared.Contains(kind));
        }

        [Fact]
        public void DisplayCandidates_AreTheSameListInTheSchemaAndTheModel()
        {
            EntityMetadata.DisplayCandidates.Should().Equal(WireNames.DisplayCandidates);
        }

        [Fact]
        public void EveryEntitysKey_IsTheOneTheModelHolds()
        {
            var build = SchemaBuild.Degraded;

            foreach (var (id, entry) in build.Document.Types.Where(entry => entry.Value.Entity == true))
            {
                var key = build.Model.Entities[id].Key?.Wire;

                if (key is null)
                    entry.Key.Should().BeNull($"{id} has no key in the model");
                else
                    entry.Key.Should().Equal([key], $"{id} publishes the key the engine answers for");
            }
        }
    }
}
