using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OxQL.Core.Engine;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Test.OxSchema.Fixtures;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The entity model behind the document: one model, shared with the query engine.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaModelTests
    {
        private static void Configure(OxSchemaOptionsBuilder schema)
        {
            var options = SchemaBuild.Options();
            schema.ServiceName = options.ServiceName;
            schema.ApiName = options.ApiName;
            schema.ApiVersion = options.ApiVersion;
            schema.TypeAssemblies = options.TypeAssemblies;
            schema.ControllerTypes = options.ControllerTypes;
            schema.EnvironmentName = "Production";
            schema.ContinuousIntegration = false;
        }

        [Fact]
        public void Build_Model_DescribesEveryEntityTheDocumentDescribes()
        {
            var registry = SchemaBuild.Degraded;

            var documentEntities = registry.Document.Types.Where(entry => entry.Value.Entity).Select(entry => entry.Key).Order(StringComparer.Ordinal);

            registry.Model.Entities.Keys.Should().Equal(documentEntities);
        }

        [Fact]
        public void Build_Pool_IsTheModelsPoolUnderTheSameIds()
        {
            var registry = SchemaBuild.Degraded;

            registry.Document.Types.Keys.Should().Equal(registry.Model.TypePool.Keys.Order(StringComparer.Ordinal));
        }

        [Fact]
        public void Build_Properties_FollowTheModelsMemberOrder()
        {
            var registry = SchemaBuild.Degraded;

            foreach (var (id, type) in registry.Model.TypePool.Where(entry => !entry.Value.IsEnum))
                registry.Document.PropertyNames(id).Should().Equal(type.Members.Select(member => member.WireName), $"{id} publishes the model's members in order");
        }

        [Fact]
        public void Build_UnstoredMember_IsInTheWireViewWithoutAStorageName()
        {
            // A get-only member is not mapped by the driver: still published, never a storage name.
            var registry = SchemaBuild.Degraded;

            registry.Model.Entities["probe.widget"].Path("label")!.Stored.Should().BeFalse();
            registry.Document.Property("probe.widget", "label").StorageName.Should().BeNull();
        }

        [Fact]
        public void Build_ModelKey_IsTheStoredIdOfEveryEntity()
        {
            var registry = SchemaBuild.Degraded;

            foreach (var entity in registry.Model.Entities.Values)
                entity.Key!.Storage.Should().Be("_id", $"{entity.Id} is stored under _id");
        }

        [Fact]
        public void AddOxSchema_HandsTheRegistrysModelToTheQueryEngine()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddSingleton<IEntityModelProvider>(new LazyEntityModelProvider(() => throw new InvalidOperationException("the backend's fallback must not win")));
            services.AddOxSchema(Configure);

            using var provider = services.BuildServiceProvider();

            var registry = provider.GetRequiredService<OxSchemaRegistry>();

            provider.GetRequiredService<IEntityModelProvider>().Model.Should().BeSameAs(registry.Model);
            provider.GetServices<IEntityModelProvider>().Should().HaveCount(1);
        }

        [Fact]
        public void Build_ModelFindings_ReachTheDocumentsFindings()
        {
            var registry = SchemaBuild.Build(SchemaBuild.OptionsWithoutAssemblies());

            registry.Model.Findings.Should().ContainSingle(finding => finding.Code == "entity-assemblies-missing");
            registry.Findings.Should().ContainSingle(finding => finding.Code == "entity-assemblies-missing" && finding.Target == SchemaBuild.Service);
        }

        [Fact]
        public void Build_Fixtures_DeclareTheExtendabilityTheAddonApiReads()
        {
            var model = SchemaBuild.Degraded.Model;

            model.Entities["probe.widget"].Extendable.Should().BeTrue();
            model.Entities["probe.thing"].Extendable.Should().BeFalse();
            typeof(WidgetModel).Should().NotBeNull();
        }
    }
}
