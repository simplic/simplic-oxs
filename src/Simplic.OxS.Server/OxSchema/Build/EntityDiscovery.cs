using System.Reflection;
using System.Text;
using OxQL.Core.Attributes;
using OxQL.Core.Registration;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>One entity the document describes: its id, its CLR type, and whether it accepts addon fields.</summary>
    internal sealed record EntityDeclaration(string Id, Type ClrType, bool Extendable);

    /// <summary>Finds the entities a service declares, through the query engine's own registry.</summary>
    internal static class EntityDiscovery
    {
        /// <summary>
        /// The entities of the scanned assemblies, ordered by id. Empty, with a published finding,
        /// when no assembly was named or the scan threw; an id more than one declaration claims is
        /// dropped for every claimant.
        /// </summary>
        /// <remarks>
        /// Discovery goes through the engine's registry rather than a second attribute scan so the
        /// document describes exactly the types the engine queries, including its rule that a
        /// declaration on a base class resolves to the most derived subclass. The registry keys
        /// ids case-insensitively with last-wins, so duplicate ids are read off the declarations,
        /// where the collision is still visible. The scan is caught in every environment: a type
        /// that fails to load in a transitive dependency is not a defect this service authored and
        /// must not stop it from starting.
        /// </remarks>
        public static IReadOnlyList<EntityDeclaration> Discover(
            IReadOnlyList<Assembly> assemblies,
            string service,
            FindingCollector findings)
        {
            if (assemblies.Count == 0)
            {
                findings.Add(
                    OxSchemaCodes.EntityAssembliesMissing,
                    service,
                    "No assemblies were named to scan, so this document describes no types at all.",
                    "The host's type assembly list is empty; the query engine scans the same list.");

                return [];
            }

            OxQLTypeRegistration[] registrations;

            try
            {
                registrations = new OxQLTypeRegistry()
                    .ScanAssemblies([.. assemblies])
                    .Registrations
                    .Where(registration => registration.ClrType is not null)
                    .OrderBy(registration => registration.TypeName, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception exception)
            {
                findings.Add(
                    OxSchemaCodes.EntityScanFailed,
                    service,
                    "The entity scan failed, so this document describes no types at all.",
                    ScanFailureDetail(exception));

                return [];
            }

            var duplicates = DuplicateIds(assemblies, findings);
            var declarations = new List<EntityDeclaration>();
            var claimed = new Dictionary<Type, string>();

            foreach (var registration in registrations)
            {
                var id = Normalize(registration.TypeName);

                if (duplicates.Contains(id))
                    continue;

                if (!claimed.TryAdd(registration.ClrType!, id))
                {
                    findings.Add(
                        OxSchemaCodes.EntityTypeShared,
                        id,
                        $"The type behind this id is already described as '{claimed[registration.ClrType!]}', so this id is not described.",
                        registration.ClrType!.FullName);

                    continue;
                }

                declarations.Add(new EntityDeclaration(id, registration.ClrType!, registration.Extendable));
            }

            return declarations;
        }

        /// <summary>The one normalisation every entity id passes through: trimmed and lower-cased.</summary>
        public static string Normalize(string id) => id.Trim().ToLowerInvariant();

        private static HashSet<string> DuplicateIds(IReadOnlyList<Assembly> assemblies, FindingCollector findings)
        {
            var claims = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            // Every type loads: the registry scan above called GetTypes() on the same assemblies and succeeded.
            foreach (var assembly in assemblies)
                foreach (var type in assembly.GetTypes())
                {
                    var declared = type.GetCustomAttribute<OxQLTypeAttribute>(inherit: false)?.TypeName;

                    if (string.IsNullOrWhiteSpace(declared))
                        continue;

                    var id = Normalize(declared);

                    if (!claims.TryGetValue(id, out var claimants))
                        claims[id] = claimants = [];

                    claimants.Add(type.FullName ?? type.Name);
                }

            var duplicates = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (id, claimants) in claims)
            {
                if (claimants.Count < 2)
                    continue;

                duplicates.Add(id);

                findings.Add(
                    OxSchemaCodes.DuplicateEntityId,
                    id,
                    $"{claimants.Count} declarations claim this id, so none of them is described.",
                    string.Join(", ", claimants.OrderBy(name => name, StringComparer.Ordinal)));
            }

            return duplicates;
        }

        private static string ScanFailureDetail(Exception exception)
        {
            var detail = new StringBuilder($"{exception.GetType().Name}: {exception.Message}");

            if (exception is ReflectionTypeLoadException load)
                foreach (var message in load.LoaderExceptions
                    .Where(inner => inner is not null)
                    .Select(inner => inner!.Message)
                    .Distinct(StringComparer.Ordinal)
                    .Where(message => !exception.Message.Contains(message, StringComparison.Ordinal))
                    .Take(5))
                    detail.Append($" | {message}");

            return detail.ToString();
        }
    }
}
