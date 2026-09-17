using OxQL.Model;
using OxQL.Model.Build;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>One entity the document describes: its id, its CLR type, and whether it accepts addon fields.</summary>
    internal sealed record EntityDeclaration(string Id, Type ClrType, bool Extendable);

    /// <summary>The entities of the query engine's model, the one set the engine and the document share.</summary>
    internal static class EntityDiscovery
    {
        /// <summary>The model's entities in id order; an entity without a CLR type (a document-built model) is not described.</summary>
        public static IReadOnlyList<EntityDeclaration> Declarations(EntityModel model) =>
        [
            .. model.Entities.Values
                .Where(entity => entity.ClrType is not null)
                .Select(entity => new EntityDeclaration(entity.Id, entity.ClrType!, entity.Extendable)),
        ];

        /// <summary>The one normalisation every entity id passes through: trimmed and lower-cased.</summary>
        public static string Normalize(string id) => WireNames.NormalizeEntityId(id);
    }
}
