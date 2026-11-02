using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxQL.AspNetCore;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The schema's continuous-integration value is the one the query engine's remote reference check reads.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class HardeningContinuousIntegrationTests
    {
        private static OxQLEndpointOptions EndpointOptions(bool continuousIntegration)
        {
            var services = new ServiceCollection();
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);

            services.AddOxSchema(schema =>
            {
                HardeningTestHost.Lenient(schema);
                schema.ContinuousIntegration = continuousIntegration;
            });
            services.AddOxQLAspNetCore();

            using var provider = services.BuildServiceProvider();

            return provider.GetRequiredService<IOptions<OxQLEndpointOptions>>().Value;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddOxSchema_TheEngineReadsTheSchemasContinuousIntegrationValue(bool continuousIntegration)
        {
            // The engine reads CI and TF_BUILD itself when it is registered; the schema's value wins
            // whatever the machine defines, so a host cannot end up strict in one check and lenient in the other.
            EndpointOptions(continuousIntegration).ContinuousIntegration.Should().Be(continuousIntegration);
        }

        [Fact]
        public void AddOxSchema_TheHostsOwnEndpointConfigurationIsKept()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);

            services.AddOxSchema(schema =>
            {
                HardeningTestHost.Lenient(schema);
                schema.ContinuousIntegration = true;
            });
            services.AddOxQLAspNetCore(endpoint => endpoint.AuthorizationPolicy = "internal");

            using var provider = services.BuildServiceProvider();
            var endpoint = provider.GetRequiredService<IOptions<OxQLEndpointOptions>>().Value;

            endpoint.ContinuousIntegration.Should().BeTrue();
            endpoint.AuthorizationPolicy.Should().Be("internal", "only the continuous-integration value is the schema's");
        }

        [Fact]
        public void AddOxSchema_ASecondCallChangesNothing()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);

            services.AddOxSchema(schema =>
            {
                HardeningTestHost.Lenient(schema);
                schema.ContinuousIntegration = true;
            });
            var registrations = services.Count;

            services.AddOxSchema(schema =>
            {
                HardeningTestHost.Lenient(schema);
                schema.ContinuousIntegration = false;
            });

            services.Count.Should().Be(registrations, "the second call registers nothing");

            services.AddOxQLAspNetCore();

            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IOptions<OxQLEndpointOptions>>().Value.ContinuousIntegration.Should().BeTrue("the first registration is the host's");
        }
    }
}
