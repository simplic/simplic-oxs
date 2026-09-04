using System.Text;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>What one build produces: the schema document, its body, every finding, and the legacy document.</summary>
    internal sealed record OxSchemaBuildResult(
        OxSchemaDocument Document,
        byte[] Body,
        IReadOnlyList<OxSchemaFinding> Findings,
        ModelDefinitionDocument? ModelDefinition);

    /// <summary>Builds the schema document and the legacy document from a host's inputs, in one pass at startup.</summary>
    internal static class OxSchemaBuilder
    {
        /// <summary>
        /// Builds the documents.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The document is ambiguous and the options fail fast. Every other finding is logged and,
        /// where a client could not detect it from absence, published in <c>diagnostics</c>.
        /// </exception>
        public static OxSchemaBuildResult Build(OxSchemaBuildOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var service = options.ServiceName.ToLowerInvariant();
            var findings = new FindingCollector();

            // Built first because the schema reads it: an entity's item collections publish the
            // legacy ids that name the same sub-object, and only ids that document really
            // publishes are used. The direction is one-way; nothing here moves a byte of it.
            var legacy = ModelDefinitionDocument.Build(options.ControllerTypes);

            var entities = EntityDiscovery.Discover(options.TypeAssemblies, service, findings);
            var keys = new Dictionary<Type, string>();
            var pool = new Dictionary<string, OxSchemaType>(StringComparer.Ordinal);

            // Every entity is in the pool before any member is walked, so a member typed as
            // another entity points at that entity rather than at a second, structural copy.
            foreach (var entity in entities)
            {
                keys[entity.ClrType] = entity.Id;
                pool[entity.Id] = new OxSchemaType { Entity = true, Properties = [] };
            }

            var walker = new TypePoolWalker(keys, pool, findings, Relationships.EntityIndex(entities.Select(entity => entity.Id)));
            var link = new ControllerLink(options.ControllerTypes);
            var controllers = link.Link(entities, findings);

            foreach (var entity in entities)
            {
                var properties = walker.DescribeProperties(entity.ClrType);
                var controller = controllers.GetValueOrDefault(entity.ClrType);

                pool[entity.Id] = pool[entity.Id] with
                {
                    Properties = properties,
                    DisplayName = EntityMetadata.TypeLabel(entity.ClrType),
                    Key = EntityMetadata.KeyOf(entity.ClrType, properties),
                    Display = EntityMetadata.DisplayOf(properties),

                    // The ids this entity retired first, then the legacy model ids its controller publishes.
                    Aliases = [.. RetiredIdsOf(options, entity.Id), .. link.AliasesOf(entity.ClrType, controller)],
                    Extendable = entity.Extendable,
                    Queryable = true,
                    NotFilterable = [],
                    NotSortable = [],
                    Operations = controller is null ? null : ControllerLink.OperationsOf(controller),
                };
            }

            // Item collections and reference fields read the finished pool.
            foreach (var entity in entities)
                pool[entity.Id] = pool[entity.Id] with { Items = ItemCollections.Of(pool, entity.Id, legacy) };

            pool = Relationships.ResolveFields(pool);

            var types = StructuralIds.Assign(pool, keys);

            DocumentValidator.Inspect(types, findings);

            var sorted = findings.Sorted();
            var refusing = sorted.Where(finding => finding.Refuses).ToList();

            if (refusing.Count > 0 && options.FailFast)
                throw new InvalidOperationException(RefusalMessage(service, refusing));

            var published = sorted.Where(finding => finding.Published).Select(finding => finding.ToDiagnostic()).ToList();
            var limits = options.QueryLimits;

            var document = new OxSchemaDocument
            {
                Service = service,
                Api = new OxSchemaApi { Name = options.ApiName, Version = options.ApiVersion },
                Limits = new OxSchemaLimits
                {
                    MaxPageSize = limits.MaxPageSize,
                    DefaultPageSize = limits.DefaultPageSize,
                    MaxPipelineStages = limits.MaxPipelineStages,
                    MaxLookupStages = limits.MaxLookupStages,
                    MaxUnwindStages = limits.MaxUnwindStages,
                    MaxGroupFields = limits.MaxGroupFields,
                    MaxProjectionFields = limits.MaxProjectionFields,
                    RegexMaxLength = limits.RegexMaxLength,
                },
                Diagnostics = published.Count > 0 ? published : null,
                Types = types,
            };

            document = document with { Revision = OxSchemaJson.Revision(document) };

            return new OxSchemaBuildResult(document, OxSchemaJson.Serialize(document), sorted, legacy);
        }

        /// <summary>The ids an entity retired, ordinally sorted: the list is inside the revision, so the host's declaration order must not reach it.</summary>
        private static IEnumerable<string> RetiredIdsOf(OxSchemaBuildOptions options, string entityId) =>
            options.RetiredEntityIds.TryGetValue(entityId, out var retired)
                ? retired.OrderBy(id => id, StringComparer.Ordinal)
                : [];

        /// <summary>The message a fail-fast host refuses with: every ambiguous finding at once.</summary>
        private static string RefusalMessage(string service, IReadOnlyList<OxSchemaFinding> refusing)
        {
            var message = new StringBuilder();

            message.Append($"Ox schema: refusing to serve '{service}' - {refusing.Count} ambiguous validation ");
            message.Append(refusing.Count == 1 ? "finding" : "findings");
            message.AppendLine(". Development, Local and CI hosts fail fast on an ambiguous document; every other");
            message.AppendLine("host logs this and serves the document with its `diagnostics` member filled.");

            foreach (var finding in refusing)
            {
                message.Append($"  {finding.Code}  {finding.Target}  {finding.Detail}");

                if (!string.IsNullOrEmpty(finding.ClrDetail))
                    message.Append($"  [{finding.ClrDetail}]");

                message.AppendLine();
            }

            return message.ToString();
        }
    }
}
