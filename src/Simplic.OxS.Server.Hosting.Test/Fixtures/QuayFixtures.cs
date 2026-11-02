using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>An entity with a polymorphic collection, in the assembly the startup names.</summary>
    [OxQLType("quay.berth", "quay.berth")]
    public class Berth : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public IList<BerthGear> Gear { get; set; } = [];
    }

    /// <summary>The base of the stored gear variants.</summary>
    public abstract class BerthGear
    {
        public Guid Id { get; set; }
    }

    /// <summary>A stored gear variant.</summary>
    public class CraneGear : BerthGear
    {
        public int Reach { get; set; }
    }

    /// <summary>A stored gear variant.</summary>
    public class RampGear : BerthGear
    {
        public bool Covered { get; set; }
    }

    /// <summary>An entity only the service's own schema registration names.</summary>
    [OxQLType("quay.mooring", "quay.mooring")]
    public class Mooring : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>
    /// A service that registers the schema itself where it registers its services, with inputs
    /// of its own, and its class maps in the mapper profiles, without a guard.
    /// </summary>
    public class QuayStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>How often the profiles were registered in this process.</summary>
        public static int ProfileRuns;

        protected override string ServiceName => "Quay";

        protected override Type[] EntityTypes => [typeof(Berth), typeof(BerthGear), typeof(CraneGear), typeof(RampGear)];

        protected override void RegisterServices(IServiceCollection services) =>
            services.AddOxSchema(schema =>
            {
                schema.ServiceName = "harbour";
                schema.ApiName = "harbour-api";
                schema.ApiVersion = "v9";
                schema.EnvironmentName = "Development";
                schema.TypeAssemblies = [new TypeSet(typeof(Mooring))];
                schema.RequireAuthorization = true;
                schema.RetireEntityId("quay.berth", "quay.dock");
            });

        protected override void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration)
        {
            Interlocked.Increment(ref ProfileRuns);

            BsonClassMap.RegisterClassMap<BerthGear>();
            BsonClassMap.RegisterClassMap<CraneGear>();
            BsonClassMap.RegisterClassMap<RampGear>();
        }
    }

    /// <summary>An entity of a service that registers the schema after its host did.</summary>
    [OxQLType("pier.bollard", "pier.bollard")]
    public class Bollard : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>A service that registers the schema itself once the host has: where it adds to the controllers.</summary>
    public class PierStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Pier";

        protected override Type[] EntityTypes => [typeof(Bollard)];

        protected override void MvcBuilder(IMvcBuilder builder)
        {
            base.MvcBuilder(builder);

            builder.Services.AddOxSchema(schema =>
            {
                schema.ServiceName = "harbour";
                schema.ApiName = "harbour-api";
                schema.ApiVersion = "v9";
            });
        }
    }

    /// <summary>An entity of a service that registers a registry of its own.</summary>
    [OxQLType("jetty.fender", "jetty.fender")]
    public class Fender : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>A service that registers a registry of its own where it registers its services.</summary>
    public class JettyStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Jetty";

        protected override Type[] EntityTypes => [typeof(Fender)];

        protected override void RegisterServices(IServiceCollection services) =>
            services.AddSingleton(_ => OxSchemaRegistry.Build(new OxSchemaOptionsBuilder
            {
                ServiceName = "harbour",
                ApiName = "harbour-api",
                ApiVersion = "v9",
            }.Build()));
    }
}
