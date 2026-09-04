using System.Reflection;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>The mutable face of <see cref="OxSchemaBuildOptions"/> a host fills at registration.</summary>
    public sealed class OxSchemaOptionsBuilder
    {
        private readonly Dictionary<string, IReadOnlyList<string>> retired = new(StringComparer.Ordinal);

        /// <inheritdoc cref="OxSchemaBuildOptions.ServiceName"/>
        public string ServiceName { get; set; } = "";

        /// <inheritdoc cref="OxSchemaBuildOptions.ApiName"/>
        public string ApiName { get; set; } = "";

        /// <inheritdoc cref="OxSchemaBuildOptions.ApiVersion"/>
        public string ApiVersion { get; set; } = "";

        /// <inheritdoc cref="OxSchemaBuildOptions.TypeAssemblies"/>
        public IReadOnlyList<Assembly> TypeAssemblies { get; set; } = [];

        /// <inheritdoc cref="OxSchemaBuildOptions.ControllerTypes"/>
        public IReadOnlyList<Type> ControllerTypes { get; set; } = [];

        /// <inheritdoc cref="OxSchemaBuildOptions.EnvironmentName"/>
        public string EnvironmentName { get; set; } = "";

        /// <inheritdoc cref="OxSchemaBuildOptions.ContinuousIntegration"/>
        public bool ContinuousIntegration { get; set; }

        /// <summary>
        /// Declares that <paramref name="currentId"/> replaced <paramref name="retiredIds"/>. The
        /// retired ids are published as aliases of the entity, normalised the way every entity id
        /// is, so a persisted configuration that still holds one keeps resolving.
        /// </summary>
        public OxSchemaOptionsBuilder RetireEntityId(string currentId, params string[] retiredIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(currentId);
            ArgumentNullException.ThrowIfNull(retiredIds);

            if (retiredIds.Length == 0 || retiredIds.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Every retired id must be a non-empty string.", nameof(retiredIds));

            retired[EntityDiscovery.Normalize(currentId)] = [.. retiredIds.Select(EntityDiscovery.Normalize)];

            return this;
        }

        /// <summary>The immutable options.</summary>
        /// <exception cref="InvalidOperationException">The service name, the api name or the api version is blank.</exception>
        public OxSchemaBuildOptions Build()
        {
            if (string.IsNullOrWhiteSpace(ServiceName) || string.IsNullOrWhiteSpace(ApiName) || string.IsNullOrWhiteSpace(ApiVersion))
                throw new InvalidOperationException("The schema needs the host's service name, api name and api version; one of them is blank.");

            return new OxSchemaBuildOptions
            {
                ServiceName = ServiceName,
                ApiName = ApiName,
                ApiVersion = ApiVersion,
                TypeAssemblies = TypeAssemblies,
                ControllerTypes = ControllerTypes,
                EnvironmentName = EnvironmentName,
                ContinuousIntegration = ContinuousIntegration,
                RetiredEntityIds = new Dictionary<string, IReadOnlyList<string>>(retired, StringComparer.Ordinal),
            };
        }
    }
}
