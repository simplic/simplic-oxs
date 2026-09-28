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

        /// <summary>
        /// The model's format 1.1 codes: polymorphism and typed references. Each marks a member or
        /// a reference case the document still describes, or leaves out in a way its absence
        /// shows, so each is log-only: neither refusing nor published.
        /// </summary>
        internal static readonly string[] LogOnly =
        [
            BuildCodes.PolymorphicMemberConflict,
            BuildCodes.PolymorphicSubtypeUnregistered,
            BuildCodes.ReferenceKeyKindMismatch,
            BuildCodes.ReferenceCaseTargetUnknown,
            BuildCodes.ReferenceItemUnknown,
            BuildCodes.ReferenceCandidateUndeclared,
        ];

        /// <summary>The published sentence of <c>entity-scan-failed</c>.</summary>
        internal const string ScanFailedDetail = "The entity scan failed, so this document describes no types at all.";

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
            BuildCodes.EntityScanFailed => ScanFailedDetail,
            _ => finding.Message,
        };
    }
}
