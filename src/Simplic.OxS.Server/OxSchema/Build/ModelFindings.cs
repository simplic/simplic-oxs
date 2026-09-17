using OxQL.Model;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Carries the model build's findings into the document's. The codes the two share keep
    /// their meaning and their cost (<see cref="OxSchemaCodes"/>); the model's own codes are
    /// log-only, because every one of them marks a member the document still describes.
    /// </summary>
    internal static class ModelFindings
    {
        /// <summary>The model findings whose target is the whole host rather than one entity or member.</summary>
        private static readonly string[] HostWide = [BuildCodes.EntityAssembliesMissing, BuildCodes.EntityScanFailed];

        /// <summary>Records every model finding, in the document's wording where the code is shared.</summary>
        public static void Import(EntityModel model, string service, FindingCollector findings)
        {
            foreach (var finding in model.Findings)
            {
                var target = finding.Target.Length == 0 && HostWide.Contains(finding.Code, StringComparer.Ordinal) ? service : finding.Target;

                findings.Add(finding.Code, target, Detail(finding), finding.Detail);
            }
        }

        private static string Detail(BuildFinding finding) => finding.Code switch
        {
            BuildCodes.EntityAssembliesMissing => "No assemblies were named to scan, so this document describes no types at all.",
            BuildCodes.EntityScanFailed => "The entity scan failed, so this document describes no types at all.",
            _ => finding.Message,
        };
    }
}
