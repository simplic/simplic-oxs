namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The one walk over a property descriptor and everything nested inside it, used by the
    /// pointer-integrity check, the structural-id rename and the item-collection walk alike.
    /// </summary>
    internal static class DescriptorVisitor
    {
        /// <summary>Every pool pointer a descriptor carries, at any depth.</summary>
        public static IEnumerable<string> Pointers(OxSchemaProperty descriptor)
        {
            if (descriptor.Type is not null)
                yield return descriptor.Type;

            if (descriptor.Of is not null)
                foreach (var pointer in Pointers(descriptor.Of))
                    yield return pointer;

            if (descriptor.Value is not null)
                foreach (var pointer in Pointers(descriptor.Value))
                    yield return pointer;
        }

        /// <summary>Every entity id a descriptor names as a snapshot source, at any depth.</summary>
        public static IEnumerable<string> SnapshotSources(OxSchemaProperty descriptor)
        {
            if (descriptor.SnapshotOf is not null)
                yield return descriptor.SnapshotOf;

            if (descriptor.Of is not null)
                foreach (var source in SnapshotSources(descriptor.Of))
                    yield return source;

            if (descriptor.Value is not null)
                foreach (var source in SnapshotSources(descriptor.Value))
                    yield return source;
        }

        /// <summary>A copy of the descriptor with every pool key it points at passed through <paramref name="rename"/>.</summary>
        public static OxSchemaProperty Repoint(OxSchemaProperty descriptor, Func<string, string> rename) =>
            descriptor with
            {
                Type = descriptor.Type is null ? null : OxSchemaPointer.To(rename(OxSchemaPointer.Strip(descriptor.Type))),
                Of = descriptor.Of is null ? null : Repoint(descriptor.Of, rename),
                Value = descriptor.Value is null ? null : Repoint(descriptor.Value, rename),
            };

        /// <summary>The descriptor's kind once array traversal is accounted for, so an array of guids is a guid member.</summary>
        public static string LeafKind(OxSchemaProperty descriptor)
        {
            var current = descriptor;

            while (current.Kind == OxSchemaKinds.Array && current.Of is not null)
                current = current.Of;

            return current.Kind;
        }
    }
}
