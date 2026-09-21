using System.Collections.Immutable;
using System.Text;
using OxQL.Model;
using OxQL.Model.Build;
using ModelDeclaration = OxQL.Model.Build.EntityDeclaration;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>What one build produces: the entity model, the schema document, its body, every finding, and the legacy document.</summary>
    internal sealed record OxSchemaBuildResult(
        EntityModel Model,
        OxSchemaDocument Document,
        byte[] Body,
        IReadOnlyList<OxSchemaFinding> Findings,
        ModelDefinitionDocument? ModelDefinition);

    /// <summary>Builds the entity model, the schema document and the legacy document from a host's inputs, in one pass at startup.</summary>
    internal static class OxSchemaBuilder
    {
        /// <summary>
        /// Builds the documents.
        /// </summary>
        /// <remarks>
        /// The entity model is walked through the MongoDB driver's serializer registry: the
        /// build looks up the serializer of every entity and of every type reachable from one,
        /// and the first lookup of a type creates its class map and freezes it. The build
        /// therefore never runs during service registration. The startup filter runs it after
        /// <c>ConfigureServices</c> and before the first request, which covers every class map a
        /// host registers while it registers its services, and nothing later: a registration in
        /// a repository's static constructor, in a hosted service or on first use meets a frozen
        /// map, where <c>RegisterClassMap</c> throws and a registration guarded by
        /// <c>IsClassMapRegistered</c> is skipped.
        /// </remarks>
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

            // One declaration set, one model, one published document: the engine binds against
            // this model and the document is its wire view.
            var model = ClrModelBuilder.Build(options.TypeAssemblies, options.RetiredEntityIds);

            ModelFindings.Import(model, service, findings);

            var entities = EntityDiscovery.Declarations(model);
            var pool = TypePoolWalker.Project(model);
            var link = new ControllerLink(options.ControllerTypes);
            var controllers = link.Link(entities, findings);

            foreach (var entity in entities)
            {
                var properties = pool[entity.Id].Properties ?? [];
                var controller = controllers.GetValueOrDefault(entity.ClrType);

                pool[entity.Id] = pool[entity.Id] with
                {
                    DisplayName = EntityMetadata.TypeLabel(entity.ClrType),

                    // The key is the model's, not a second derivation from the identity
                    // interfaces: the engine answers for the one the model holds, and a
                    // reference's `field` already defaults to it.
                    Key = model.Entities[entity.Id].Key is { } key
                        ? [key.Wire]
                        : EntityMetadata.KeyOf(entity.ClrType, properties),
                    Display = EntityMetadata.DisplayOf(properties),

                    // The ids this entity retired first, then the legacy model ids its controller publishes.
                    Aliases = [.. RetiredIdsOf(options, entity.Id), .. link.AliasesOf(entity.ClrType, controller)],
                    Extendable = entity.Extendable,
                    Queryable = true,

                    // A member the driver does not store is in the wire view and refused by the
                    // engine with NOT_STORED. Saying so here is what keeps a consumer from
                    // offering a filter the service will not answer.
                    NotFilterable = UnstoredScalarPaths(model, entity.Id),

                    // Everything that makes a stored scalar unsortable - crossing a collection -
                    // is already visible in the descriptors, and every consumer derives it.
                    NotSortable = [],
                    Operations = controller is null ? null : ControllerLink.OperationsOf(controller),
                };
            }

            // Item collections read the finished pool.
            foreach (var entity in entities)
                pool[entity.Id] = pool[entity.Id] with { Items = ItemCollections.Of(pool, entity.Id, legacy) };

            var types = ImmutableSortedDictionary.CreateRange(StringComparer.Ordinal, pool);

            DocumentValidator.Inspect(types, findings);

            var sorted = findings.Sorted();
            var refusing = sorted.Where(finding => finding.Refuses).ToList();

            if (refusing.Count > 0 && options.FailFast)
                throw new InvalidOperationException(RefusalMessage(service, refusing));

            return Compose(options, service, model, types, sorted, legacy);
        }

        /// <summary>
        /// Builds the documents of a host whose build threw: no types, the
        /// <c>entity-scan-failed</c> diagnostic, and the legacy document where it can still be
        /// generated. Reads none of the host's entity declarations, so the input that made the
        /// build throw cannot make this throw.
        /// </summary>
        /// <param name="options">The inputs of the build that threw.</param>
        /// <param name="cause">What the build threw; it reaches the log only, never the wire.</param>
        public static OxSchemaBuildResult BuildDegraded(OxSchemaBuildOptions options, Exception cause)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(cause);

            var service = options.ServiceName.ToLowerInvariant();
            var findings = new FindingCollector();

            findings.Add(OxSchemaCodes.EntityScanFailed, service, ModelFindings.ScanFailedDetail, $"{cause.GetType().Name}: {cause.Message}");

            var model = ClrModelBuilder.Build(Array.Empty<ModelDeclaration>());
            var types = ImmutableSortedDictionary.Create<string, OxSchemaType>(StringComparer.Ordinal);

            return Compose(options, service, model, types, findings.Sorted(), LegacyOrNull(options));
        }

        /// <summary>The legacy document, or null when even that cannot be generated; the endpoint then answers 404.</summary>
        private static ModelDefinitionDocument? LegacyOrNull(OxSchemaBuildOptions options)
        {
            try
            {
                return ModelDefinitionDocument.Build(options.ControllerTypes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Assembles the document from a finished pool, stamps its revision and serialises it.</summary>
        private static OxSchemaBuildResult Compose(
            OxSchemaBuildOptions options,
            string service,
            EntityModel model,
            ImmutableSortedDictionary<string, OxSchemaType> types,
            IReadOnlyList<OxSchemaFinding> sorted,
            ModelDefinitionDocument? legacy)
        {
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

                    // The rest of the engine's limits are published on /oxql/health; these are
                    // the ones a caller checks a request against before it sends one.
                    MaxOffset = limits.Limits.MaxOffset,
                    MaxResolveStages = limits.Limits.MaxResolveStages,
                    MaxBatchQueries = limits.Limits.MaxBatchQueries,
                    MaxLookupLimit = limits.Limits.MaxLookupLimit,
                },
                Diagnostics = published.Count > 0 ? published : null,
                Types = types,
            };

            document = document with { Revision = OxSchemaJson.Revision(document) };

            return new OxSchemaBuildResult(model, document, OxSchemaJson.Serialize(document), sorted, legacy);
        }

        /// <summary>
        /// The scalar paths of an entity the driver does not store: in the wire view, refused by
        /// the query engine. Ordinally sorted, because the list is inside the revision.
        /// </summary>
        private static IReadOnlyList<string> UnstoredScalarPaths(EntityModel model, string entityId) =>
            model.Entities.TryGetValue(entityId, out var entity)
                ? [.. entity.Paths.Where(path => !path.Stored && Kinds.IsScalar(path.LeafKind)).Select(path => path.Wire).Order(StringComparer.Ordinal)]
                : [];

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
