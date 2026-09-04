using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>Writes the startup log of a built registry: one summary line, one line per finding, and the legacy document's failures.</summary>
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
                    finding.Refuses || finding.Published ? LogLevel.Error : LogLevel.Warning,
                    "Ox schema finding: {Code} {Target} - {Detail} (refuses={Refuses} published={Published}){ClrDetail}",
                    finding.Code,
                    finding.Target,
                    finding.Detail,
                    finding.Refuses,
                    finding.Published,
                    string.IsNullOrEmpty(finding.ClrDetail) ? "" : $" [{finding.ClrDetail}]");

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
    }
}
