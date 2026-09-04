using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Test.OxSchema.Fixtures;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>
    /// Names the collection every schema test belongs to.
    /// </summary>
    /// <remarks>Every class shares one lazily built registry, so the suite runs as one collection.</remarks>
    [CollectionDefinition(SchemaCollection.Name, DisableParallelization = true)]
    public sealed class SchemaCollection
    {
        /// <summary>The collection name every schema test class carries.</summary>
        public const string Name = "Ox schema";
    }

    /// <summary>
    /// Builds schema documents over the fixture types.
    /// </summary>
    internal static class SchemaBuild
    {
        private static readonly Lazy<OxSchemaRegistry> Lenient = new(() => OxSchemaRegistry.Build(Options()));

        /// <summary>The service name the fixture host declares.</summary>
        internal const string Service = "probe";

        /// <summary>A document built in an environment that degrades, outside continuous integration.</summary>
        internal static OxSchemaRegistry Degraded => Lenient.Value;

        /// <summary>The inputs the fixture host hands the build.</summary>
        internal static OxSchemaBuildOptions Options(string environmentName = "") => new()
        {
            ServiceName = Service,
            ApiName = "probe-api",
            ApiVersion = "v1",
            TypeAssemblies = [typeof(Thing).Assembly],
            EnvironmentName = environmentName,

            // The link and the spare gadget are deliberately unlisted: an entity with no
            // controller publishes no operations member at all.
            ControllerTypes =
            [
                typeof(WidgetController),
                typeof(ThingRestController),
                typeof(GadgetController),
                typeof(GadgetMirrorController),
            ],
        };

        /// <summary>The same inputs, with an assembly whose type list cannot be loaded added.</summary>
        internal static OxSchemaBuildOptions OptionsWithUnloadableAssembly(string environmentName = "")
        {
            return Options(environmentName) with { TypeAssemblies = [typeof(Thing).Assembly, new UnloadableAssembly()] };
        }

        /// <summary>The same inputs, with no assembly named to scan.</summary>
        internal static OxSchemaBuildOptions OptionsWithoutAssemblies(string environmentName = "")
        {
            return Options(environmentName) with { TypeAssemblies = [] };
        }

        /// <summary>Builds a document outside continuous integration.</summary>
        internal static OxSchemaRegistry Build(string environmentName = "") =>
            OxSchemaRegistry.Build(Options(environmentName));

        /// <summary>Builds a document from the given inputs.</summary>
        internal static OxSchemaRegistry Build(OxSchemaBuildOptions options) =>
            OxSchemaRegistry.Build(options);

        /// <summary>The fixture inputs under a continuous-integration variable set to <paramref name="ci"/>.</summary>
        internal static OxSchemaBuildOptions OptionsUnderCi(string? ci, string environmentName = "") =>
            Options(environmentName) with { ContinuousIntegration = OxSchemaBuildOptions.ReadContinuousIntegration(ci) };
    }

    /// <summary>Reaches into a built document without repeating the null checks.</summary>
    internal static class SchemaLookup
    {
        /// <summary>The pool entry under <paramref name="id"/>.</summary>
        internal static OxSchemaType Entry(this OxSchemaDocument document, string id)
        {
            document.Types.Should().ContainKey(id);

            return document.Types[id];
        }

        /// <summary>The property named <paramref name="name"/> on the entry under <paramref name="id"/>.</summary>
        internal static OxSchemaProperty Property(this OxSchemaDocument document, string id, string name)
        {
            var property = document.Entry(id).Properties?.SingleOrDefault(member => member.Name == name);

            property.Should().NotBeNull($"{id}#{name} is described");

            return property!;
        }

        /// <summary>The names of the properties on the entry under <paramref name="id"/>, in order.</summary>
        internal static IReadOnlyList<string> PropertyNames(this OxSchemaDocument document, string id) =>
            [.. (document.Entry(id).Properties ?? []).Select(property => property.Name ?? "")];

        /// <summary>The pool key a pointer names.</summary>
        internal static string? Target(this OxSchemaProperty descriptor) =>
            descriptor.Type is null ? null : OxSchemaPointer.Strip(descriptor.Type);
    }
}
