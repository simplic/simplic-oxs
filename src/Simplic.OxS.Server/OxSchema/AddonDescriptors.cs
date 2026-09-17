using System.Security.Cryptography;
using System.Text.Json;
using OxQL.Model;
using OxQL.Model.Addon;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The organisation's addon definitions in the schema's descriptor format, served under
    /// <c>GET /schema/addons</c> as <c>{ "&lt;entityId&gt;": [ descriptor, … ] }</c>: one list
    /// per extendable entity of the model, each entry a property descriptor exactly as the
    /// document's <c>properties</c> carry them, the list being the properties of the entity's
    /// <c>addon</c> member. Definitions are per organisation, so they are not in the schema
    /// document and not in its revision; the body carries its own entity tag.
    /// </summary>
    public static class AddonDescriptors
    {
        /// <summary>The body and its entity tag for one organisation.</summary>
        public sealed record Result(byte[] Body, string ETag);

        /// <summary>Builds the body for every extendable entity of the model, in entity id order.</summary>
        public static async Task<Result> BuildAsync(EntityModel model, IAddonDefinitionSource source, Guid organisation, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(source);

            var entities = new SortedDictionary<string, IReadOnlyList<OxSchemaProperty>>(StringComparer.Ordinal);

            foreach (var entity in model.Entities.Values)
            {
                if (!entity.Extendable)
                    continue;

                var definitions = await source.ForEntityAsync(entity.Id, organisation, cancellationToken);

                entities[entity.Id] = Describe(definitions);
            }

            var body = JsonSerializer.SerializeToUtf8Bytes(entities, OxSchemaJson.Canonical);

            return new Result(body, "\"" + Convert.ToHexStringLower(SHA256.HashData(body)) + "\"");
        }

        /// <summary>
        /// The live definitions as descriptors, in path order: the name is the definition path
        /// verbatim, the kind is the definition's (an <c>object</c> container has no typing of
        /// its own and is <c>unknown</c>, as the engine reads it), a value is always readable as
        /// null, and a closed value list travels inline.
        /// </summary>
        public static IReadOnlyList<OxSchemaProperty> Describe(IEnumerable<AddonDefinition> definitions) =>
        [
            .. definitions
                .Where(definition => !definition.Retired)
                .OrderBy(definition => definition.Path, StringComparer.Ordinal)
                .Select(definition => new OxSchemaProperty
                {
                    Name = definition.Path,
                    Kind = Kinds.NameOf(AddonKinds.ToKind(definition.Kind)),
                    Nullable = true,
                    DisplayName = definition.DisplayName,
                    Description = definition.Description,
                    Values = definition.Values is { Count: > 0 } values
                        ? [.. values.Select(value => new OxSchemaValue { Value = value.Value, Label = value.Label })]
                        : null,
                }),
        ];
    }
}
