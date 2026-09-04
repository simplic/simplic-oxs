using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxQL.Core.Models;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>How a host registers the schema, and what the registry publishes from the container.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaHostingTests
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

        private static ServiceCollection Services()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
            return services;
        }

        [Fact]
        public void AddOxSchema_WithoutAConfigureAction_Throws()
        {
            var add = () => Services().AddOxSchema(null!);

            add.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddOxSchema_RegistersOneRegistryAndOneStartupFilter()
        {
            var services = Services();

            services.AddOxSchema(Configure);
            services.AddOxSchema(Configure);

            services.Count(descriptor => descriptor.ServiceType == typeof(OxSchemaRegistry)).Should().Be(1);
            services.Count(descriptor => descriptor.ServiceType == typeof(IStartupFilter)).Should().Be(1);
        }

        [Fact]
        public void AddOxSchema_PublishesTheLimitsOfTheRegisteredQueryEngineOptions()
        {
            var services = Services();
            services.AddSingleton(new OxQLOptions { MaxPageSize = 11, DefaultPageSize = 12, MaxPipelineStages = 13, MaxLookupStages = 14, MaxUnwindStages = 15, MaxGroupFields = 16, MaxProjectionFields = 17, RegexMaxLength = 18 });
            services.AddOxSchema(Configure);

            var limits = services.BuildServiceProvider().GetRequiredService<OxSchemaRegistry>().Document.Limits;

            limits.Should().BeEquivalentTo(new OxSchemaLimits
            {
                MaxPageSize = 11, DefaultPageSize = 12, MaxPipelineStages = 13, MaxLookupStages = 14,
                MaxUnwindStages = 15, MaxGroupFields = 16, MaxProjectionFields = 17, RegexMaxLength = 18,
            });
        }

        [Fact]
        public void AddOxSchema_PublishesTheLimitsOfQueryEngineOptionsBoundThroughIOptions()
        {
            var services = Services();
            services.AddSingleton<IOptions<OxQLOptions>>(Options.Create(new OxQLOptions { MaxPageSize = 21 }));
            services.AddOxSchema(Configure);

            services.BuildServiceProvider().GetRequiredService<OxSchemaRegistry>().Document.Limits.MaxPageSize.Should().Be(21);
        }

        [Fact]
        public void AddOxSchema_WithoutAQueryEngine_PublishesTheEnginesDefaults()
        {
            var services = Services();
            services.AddOxSchema(Configure);

            services.BuildServiceProvider().GetRequiredService<OxSchemaRegistry>().Document.Limits.MaxPageSize.Should().Be(new OxQLOptions().MaxPageSize);
        }

        [Fact]
        public void TheStartupFilter_BuildsTheRegistryBeforeTheHostConfigures()
        {
            var services = Services();
            services.AddOxSchema(Configure);
            var provider = services.BuildServiceProvider();
            var filter = provider.GetRequiredService<IStartupFilter>();
            var built = false;

            filter.Configure(_ => built = provider.GetRequiredService<OxSchemaRegistry>() is not null)(new ApplicationBuilder(provider));

            built.Should().BeTrue();
        }

        [Fact]
        public void TheRegistry_IsBuiltOnceAndLoggedOnce()
        {
            var services = Services();
            services.AddOxSchema(Configure);
            var provider = services.BuildServiceProvider();

            var first = provider.GetRequiredService<OxSchemaRegistry>();
            var second = provider.GetRequiredService<OxSchemaRegistry>();

            second.Should().BeSameAs(first);
            first.MarkLogged().Should().BeTrue();
            first.MarkLogged().Should().BeFalse();
        }
    }
}
