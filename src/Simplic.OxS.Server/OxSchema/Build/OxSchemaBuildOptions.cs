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

        /// <summary>Whether the host runs under a continuous-integration system.</summary>
        public bool ContinuousIntegration { get; init; }

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

        /// <summary>Reads the conventional <c>CI</c> environment variable: set and neither <c>0</c> nor <c>false</c> means a continuous-integration host.</summary>
        public static bool ReadContinuousIntegration(string? variable) =>
            !string.IsNullOrWhiteSpace(variable)
            && !string.Equals(variable, "0", StringComparison.Ordinal)
            && !string.Equals(variable, "false", StringComparison.OrdinalIgnoreCase);
    }
}
