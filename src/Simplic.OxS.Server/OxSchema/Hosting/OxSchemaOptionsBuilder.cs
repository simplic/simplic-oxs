using System.Reflection;
using OxQL.Model.Attributes;
using OxQL.Model.Build;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>The mutable face of <see cref="OxSchemaBuildOptions"/> a host fills at registration.</summary>
    public sealed class OxSchemaOptionsBuilder
    {
        private readonly Dictionary<string, IReadOnlyList<string>> retired = new(StringComparer.Ordinal);
        private readonly ReferenceDeclarations references = new();

        /// <summary>
        /// The path of <see cref="DeclareReferenceWhen{T}(string, string, string, string?, string[])"/>
        /// that conditions a case on the stored variant of the object holding the member.
        /// </summary>
        public const string Variant = ReferenceDeclarations.Variant;

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

        /// <inheritdoc cref="OxSchemaBuildOptions.RequireAuthorization"/>
        public bool RequireAuthorization { get; set; }

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

        /// <summary>
        /// Declares an unconditional reference on the wire member <paramref name="wireMember"/> of
        /// the pooled type <typeparamref name="T"/>, for a member the service cannot annotate with
        /// <see cref="OxQLReferenceAttribute"/> (an inherited <c>Id</c>, a type from a shared
        /// package). It applies to <typeparamref name="T"/> and its variants wherever they are
        /// embedded, and never to another type that inherits the same CLR member.
        /// </summary>
        /// <param name="wireMember">The member's wire name, e.g. <c>id</c>.</param>
        /// <param name="target">The target entity id, or <c>entity#itemPath</c> for an element of one of its arrays.</param>
        /// <param name="field">
        /// The path the value matches. Null for the target's key, only for a target of this
        /// service: a target in another service needs it, because this host cannot read that
        /// entity's key; without it no reference is emitted and the build logs
        /// <c>reference-target-field-unknown</c>.
        /// </param>
        /// <param name="item">The item path, as the alternative to spelling it into <paramref name="target"/>.</param>
        /// <param name="keyAs">How a stored string becomes the target's key.</param>
        /// <remarks>
        /// A member that gets both this and <see cref="DeclareReferenceWhen{T}(string, string, string, string?, string[])"/>,
        /// or also carries a reference attribute, keeps no reference and is logged as
        /// <c>reference-declaration-unresolved</c>.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="field"/> is spelt like a target (<c>entity#itemPath</c>).</exception>
        public OxSchemaOptionsBuilder DeclareReference<T>(string wireMember, string target, string? field = null, string? item = null, OxQLKeyAs keyAs = OxQLKeyAs.None)
        {
            RejectTargetAsField(field);

            references.For<T>(wireMember).To(target, field, item, keyAs);

            return this;
        }

        /// <summary>
        /// Declares one case of a reference on the wire member <paramref name="wireMember"/> of the
        /// pooled type <typeparamref name="T"/>: the member names <paramref name="targets"/>, tried
        /// in order, when the sibling <paramref name="path"/> holds <paramref name="equals"/>, or,
        /// with <see cref="Variant"/> as the path, when the holding object is stored as that
        /// variant. What <see cref="OxQLReferenceWhenAttribute"/> declares on a member the service
        /// owns. Calls on the same member form one reference with several cases.
        /// </summary>
        /// <param name="wireMember">The member's wire name, e.g. <c>id</c>.</param>
        /// <param name="path">The sibling's wire name, or <see cref="Variant"/>.</param>
        /// <param name="equals">The value the sibling is compared with exactly, or the variant name.</param>
        /// <param name="field">
        /// The path the value matches; required when a target is another service's. Positional
        /// before the targets: pass null for the targets' key rather than leaving it out, or the
        /// first target is taken for the field.
        /// </param>
        /// <param name="targets">The targets, each <c>entity</c> or <c>entity#itemPath</c>.</param>
        /// <exception cref="ArgumentException"><paramref name="field"/> is spelt like a target (<c>entity#itemPath</c>), the sign of a left-out field.</exception>
        public OxSchemaOptionsBuilder DeclareReferenceWhen<T>(string wireMember, string path, string equals, string? field, params string[] targets) =>
            DeclareReferenceWhen<T>(wireMember, path, equals, field, OxQLKeyAs.None, targets);

        /// <summary>
        /// <see cref="DeclareReferenceWhen{T}(string, string, string, string?, string[])"/> for a
        /// member whose stored value needs <paramref name="keyAs"/> to become the targets' key.
        /// </summary>
        public OxSchemaOptionsBuilder DeclareReferenceWhen<T>(string wireMember, string path, string equals, string? field, OxQLKeyAs keyAs, params string[] targets)
        {
            ArgumentNullException.ThrowIfNull(targets);
            RejectTargetAsField(field);

            references.For<T>(wireMember).When(path, equals, targets, field, keyAs);

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
                RequireAuthorization = RequireAuthorization,
                RetiredEntityIds = new Dictionary<string, IReadOnlyList<string>>(retired, StringComparer.Ordinal),
                ReferenceDeclarations = Snapshot(references),
            };
        }

        /// <summary>
        /// Refuses a field spelt like a target. A path never holds <c>#</c>, an item target does:
        /// such a field is a target that took the field's position, and the reference would lose it.
        /// </summary>
        private static void RejectTargetAsField(string? field)
        {
            if (field is not null && field.Contains('#', StringComparison.Ordinal))
                throw new ArgumentException(
                    $"The field '{field}' is spelt like a target (entity#itemPath). Pass the field, or null for the target's key, before the targets.",
                    nameof(field));
        }

        /// <summary>A copy of the declarations, so a later call on this builder cannot change options already built.</summary>
        private static ReferenceDeclarations Snapshot(ReferenceDeclarations source)
        {
            var copy = new ReferenceDeclarations();

            foreach (var declaration in source.All)
            {
                var member = copy.For(declaration.Type, declaration.WireMember);

                if (declaration.Path is null)
                    member.To(declaration.Targets[0], declaration.Field, keyAs: declaration.KeyAs);
                else
                    member.When(declaration.Path, declaration.Value ?? "", declaration.Targets, declaration.Field, declaration.KeyAs);
            }

            return copy;
        }
    }
}
