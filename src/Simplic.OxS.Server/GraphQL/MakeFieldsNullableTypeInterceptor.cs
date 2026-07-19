using HotChocolate.Configuration;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Configurations;

namespace Simplic.OxS.Server.GraphQL
{
    /// <summary>
    /// HotChocolate type interceptor that rewrites every output field of every
    /// user-defined object type so its outermost type becomes nullable.
    /// <para>
    /// Purpose: tolerate items that are missing values for fields the schema
    /// would otherwise mark as <c>NonNull</c>. Without this interceptor, a
    /// resolver returning <c>null</c> for a <c>!</c>-field causes the parent
    /// selection-set to be replaced by <c>null</c> (per GraphQL spec).
    /// </para>
    /// <para>
    /// HotChocolate built-in types (introspection, paging connections, etc.)
    /// are skipped because clients depend on their non-null guarantees.
    /// </para>
    /// </summary>
    internal sealed class MakeFieldsNullableTypeInterceptor : TypeInterceptor
    {
        public override void OnBeforeCompleteName(
            ITypeCompletionContext completionContext,
            TypeSystemConfiguration configuration)
        {
            if (completionContext.IsIntrospectionType)
                return;

            if (configuration is not ObjectTypeConfiguration objectConfig)
                return;

            // Skip HotChocolate's own types (Connection, Edge, PageInfo, ...).
            var runtimeType = objectConfig.RuntimeType;
            if (runtimeType?.Namespace is { } ns &&
                ns.StartsWith("HotChocolate", System.StringComparison.Ordinal))
            {
                return;
            }

            foreach (var field in objectConfig.Fields)
            {
                if (field.IsIntrospectionField)
                    continue;

                if (field.Type is not ExtendedTypeReference extRef)
                    continue;

                var nullableType = completionContext.TypeInspector
                    .ChangeNullability(extRef.Type, true);

                field.Type = extRef.WithType(nullableType);
            }
        }
    }
}
