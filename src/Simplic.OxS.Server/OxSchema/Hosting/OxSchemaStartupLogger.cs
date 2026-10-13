using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Writes the startup log of a built registry: one summary line, one line per finding, one
    /// line that says what to do for each kind of finding a service has to act on, and the
    /// legacy document's failures.
    /// </summary>
    internal static class OxSchemaStartupLogger
    {
        /// <summary>Resolves the registry, which builds it if necessary, and logs it once.</summary>
        public static void Log(IServiceProvider provider)
        {
            var registry = provider.GetRequiredService<OxSchemaRegistry>();

            if (!registry.MarkLogged())
                return;

            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Simplic.OxS.Server.OxSchema");
            var document = registry.Document;

            if (provider.GetService<OxSchemaRegistration>()?.Warning() is { } registeredTwice)
                logger.LogWarning("{Warning}", registeredTwice);

            logger.LogInformation(
                "Ox schema built: service={Service} api={Api}/{Version} types={Types} revision={Revision} bytes={Bytes} "
                + "findings={Findings} maxPageSize={MaxPageSize} defaultPageSize={DefaultPageSize}",
                document.Service,
                document.Api.Name,
                document.Api.Version,
                document.Types.Count,
                registry.Revision,
                registry.Body.Length,
                registry.Findings.Count,
                document.Limits.MaxPageSize,
                document.Limits.DefaultPageSize);

            foreach (var finding in registry.Findings)
                logger.Log(
                    LevelOf(finding),
                    "Ox schema finding: {Code} {Target} - {Detail} (refuses={Refuses} published={Published}){ClrDetail}",
                    finding.Code,
                    finding.Target,
                    finding.Detail,
                    finding.Refuses,
                    finding.Published,
                    string.IsNullOrEmpty(finding.ClrDetail) ? "" : $" [{finding.ClrDetail}]");

            LogWhatToDo(logger, registry.Findings);

            if (registry.ModelDefinition is not { } legacy)
                return;

            logger.LogInformation(
                "Ox schema model definition: definitions={Definitions} bytes={Bytes} failures={Failures}",
                legacy.DefinitionCount,
                legacy.Body.Length,
                legacy.Failures.Count);

            foreach (var failure in legacy.Failures)
                logger.LogWarning("Ox schema model definition dropped a controller: {Failure}", failure);
        }

        /// <summary>
        /// One error line per kind of finding the service has to act on, after the findings
        /// themselves: how many there are, what they are about, and what to do.
        /// </summary>
        private static void LogWhatToDo(ILogger logger, IReadOnlyList<OxSchemaFinding> findings)
        {
            var subtypes = findings.Where(finding => finding.Code == global::OxQL.Model.BuildCodes.PolymorphicSubtypeUnregistered).ToList();

            if (subtypes.Count > 0)
                logger.LogError(
                    "Ox schema: {Count} stored subtypes have no MongoDB class map and are missing from the schema ({Subtypes}). "
                    + "Register their class maps in Bootstrap.RegisterBsonClassMaps, or list them in [BsonKnownTypes] on the base type: "
                    + "a class map registered after the schema is built throws or is not applied.",
                    subtypes.Count,
                    Listed(subtypes.Select(finding => finding.ClrDetail ?? finding.Target)));

            var ids = findings.Where(finding => finding.Code is OxSchemaCodes.EntityIdOffGrammar or OxSchemaCodes.EntityIdForeignService).ToList();

            // A service whose name can prefix no id is told so by a finding of its own, and no rename makes its entities reachable.
            if (ids.Count > 0 && !findings.Any(finding => finding.Code == OxSchemaCodes.ServiceNameNoIdPrefix))
                logger.LogError(
                    "Ox schema: {Count} entity ids are not <service>.<entity> with this service's name as the prefix ({Ids}), so another service cannot reference them. "
                    + "Rename each id in its [OxQLType] and paste the RetireEntityId line of its finding into ConfigureOxSchema: the old id keeps resolving. "
                    + "The host starts as before.",
                    ids.Count,
                    Listed(ids.Select(finding => finding.Target)));
        }

        /// <summary>The first names of a list, and how many more there are.</summary>
        private static string Listed(IEnumerable<string> names)
        {
            const int shown = 10;
            var all = names.ToList();

            return all.Count <= shown ? string.Join(", ", all) : $"{string.Join(", ", all.Take(shown))} and {all.Count - shown} more";
        }

        /// <summary>
        /// A refusing or published finding is an error of the host, with one exception: a
        /// service that has no entities names no assemblies on purpose, so its published
        /// <c>entity-assemblies-missing</c> is a warning on every start, not an error. So is a
        /// finding the service has to act on (<see cref="OxSchemaCodes.IsActionable"/>); every
        /// other finding is a warning.
        /// </summary>
        private static LogLevel LevelOf(OxSchemaFinding finding)
        {
            if (string.Equals(finding.Code, OxSchemaCodes.EntityAssembliesMissing, StringComparison.Ordinal))
                return LogLevel.Warning;

            return finding.Refuses || finding.Published || OxSchemaCodes.IsActionable(finding.Code) ? LogLevel.Error : LogLevel.Warning;
        }
    }
}
