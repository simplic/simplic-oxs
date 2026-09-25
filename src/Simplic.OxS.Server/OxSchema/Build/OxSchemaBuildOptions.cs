using System.Reflection;
using OxQL.Core.Models;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>The inputs a schema build reads. Immutable; a host fills them through <see cref="OxSchemaOptionsBuilder"/>.</summary>
    public sealed record OxSchemaBuildOptions
    {
        /// <summary>The service name as the host declares it; lower-cased on the wire.</summary>
        public required string ServiceName { get; init; }

        /// <summary>The first segment of the service's API base path, e.g. <c>vehicle-api</c>.</summary>
        public required string ApiName { get; init; }

        /// <summary>The second segment of the base path, e.g. <c>v2</c>.</summary>
        public required string ApiVersion { get; init; }

        /// <summary>The query engine's options, so the document publishes the limits the engine enforces.</summary>
        public OxQLOptions QueryLimits { get; init; } = new();

        /// <summary>The assemblies carrying entity declarations; the same set the query engine scans.</summary>
        public IReadOnlyList<Assembly> TypeAssemblies { get; init; } = [];

        /// <summary>The controllers the host publishes model definitions for; the source of entity operations.</summary>
        public IReadOnlyList<Type> ControllerTypes { get; init; } = [];

        /// <summary>The host environment name.</summary>
        public string EnvironmentName { get; init; } = "";

        /// <summary>
        /// Whether the host runs under a continuous-integration system: the <c>CI</c> or the
        /// <c>TF_BUILD</c> environment variable is set. The query engine's startup check of the
        /// model's remote references decides its own fail-fast from this value too.
        /// </summary>
        public bool ContinuousIntegration { get; init; }

        /// <summary>
        /// Whether <c>GET /schema</c> requires an authenticated caller. Off by default: the
        /// document is organisation-independent and is fetched without credentials by build
        /// tooling and client generators, the same posture as <c>/ModelDefinition</c>. It never
        /// changes a byte of the document.
        /// </summary>
        public bool RequireAuthorization { get; init; }

        /// <summary>Current entity id to the ids it retired, for the entities of this service that renamed theirs.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> RetiredEntityIds { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        /// <summary>
        /// Whether an ambiguous document stops the host from starting: in <c>Development</c>, <c>Local</c> and under continuous
        /// integration. Every other host logs the findings and serves the document, because a metadata defect must not take a running service down.
        /// </summary>
        public bool FailFast =>
            StrictEnvironments.Contains(EnvironmentName, StringComparer.OrdinalIgnoreCase) || ContinuousIntegration;

        private static readonly string[] StrictEnvironments = ["Development", "Local"];

        /// <summary>
        /// The environment variables that mark a continuous-integration host: the conventional
        /// <c>CI</c>, and <c>TF_BUILD</c>, which is the one Azure Pipelines sets.
        /// </summary>
        internal static readonly string[] ContinuousIntegrationVariables = ["CI", "TF_BUILD"];

        /// <summary>Whether any of <see cref="ContinuousIntegrationVariables"/> marks a continuous-integration host, read through <paramref name="read"/>.</summary>
        internal static bool ReadContinuousIntegration(Func<string, string?> read) =>
            ContinuousIntegrationVariables.Any(name => ReadContinuousIntegration(read(name)));

        /// <summary>Reads one continuous-integration variable: set and neither <c>0</c> nor <c>false</c> means a continuous-integration host.</summary>
        public static bool ReadContinuousIntegration(string? variable) =>
            !string.IsNullOrWhiteSpace(variable)
            && !string.Equals(variable, "0", StringComparison.Ordinal)
            && !string.Equals(variable, "false", StringComparison.OrdinalIgnoreCase);
    }
}
