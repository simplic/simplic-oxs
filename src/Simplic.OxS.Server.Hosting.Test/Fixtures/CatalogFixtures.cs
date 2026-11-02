using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using OxQL.Core.Attributes;
using OxQL.Model.Attributes;
using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>An entity with a polymorphic collection and references of every outcome.</summary>
    [OxQLType("catalog.product", "catalog.product")]
    public class Product : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public IList<ProductPart> Parts { get; set; } = [];

        /// <summary>Into an entity of this service.</summary>
        [OxQLReference("catalog.brand")]
        public Guid BrandId { get; set; }

        /// <summary>Into another service, with the field a remote target needs.</summary>
        [OxQLReference("stock.bin", "id")]
        public Guid BinId { get; set; }

        /// <summary>Into another service without the field: the build drops it and says so.</summary>
        [OxQLReference("stock.shelf")]
        public Guid ShelfId { get; set; }
    }

    /// <summary>The target of the local reference.</summary>
    [OxQLType("catalog.brand", "catalog.brand")]
    public class Brand : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>The base of the stored part variants.</summary>
    public abstract class ProductPart
    {
        public Guid Id { get; set; }
    }

    /// <summary>A variant registered in the class-map hook.</summary>
    public class ScrewPart : ProductPart
    {
        public int Length { get; set; }
    }

    /// <summary>A variant registered in the mapper profiles.</summary>
    public class BoltPart : ProductPart
    {
        public int Diameter { get; set; }
    }

    /// <summary>A variant the service forgot to register.</summary>
    public class GluePart : ProductPart
    {
        public int Grams { get; set; }
    }

    /// <summary>A service with unguarded registrations in the hook and in its mapper profiles.</summary>
    public class CatalogStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>How often the hook, the profiles and the service registration ran in this process.</summary>
        public static int HookRuns, ProfileRuns, ServiceRuns;

        protected override string ServiceName => "Catalog";

        protected override Type[] EntityTypes =>
            [typeof(Product), typeof(Brand), typeof(ProductPart), typeof(ScrewPart), typeof(BoltPart), typeof(GluePart)];

        protected override void RegisterBsonClassMaps()
        {
            Interlocked.Increment(ref HookRuns);

            BsonClassMap.RegisterClassMap<ProductPart>();
            BsonClassMap.RegisterClassMap<ScrewPart>();
        }

        protected override void RegisterServices(IServiceCollection services) => Interlocked.Increment(ref ServiceRuns);

        protected override void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration)
        {
            Interlocked.Increment(ref ProfileRuns);

            BsonClassMap.RegisterClassMap<BoltPart>();
        }
    }

    /// <summary>An entity with a polymorphic member.</summary>
    [OxQLType("press.sheet", "press.sheet")]
    public class Sheet : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public SheetCoat? Coat { get; set; }
    }

    /// <summary>The base of the stored coat variants.</summary>
    public abstract class SheetCoat
    {
        public string? Colour { get; set; }
    }

    /// <summary>A stored coat variant.</summary>
    public class MattCoat : SheetCoat
    {
        public int Layers { get; set; }
    }

    /// <summary>A service that registers its class maps where it registers its services, guarded for a second call.</summary>
    public class PressStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>How often the service registration ran in this process.</summary>
        public static int ServiceRuns;

        protected override string ServiceName => "Press";

        protected override Type[] EntityTypes => [typeof(Sheet), typeof(SheetCoat), typeof(MattCoat)];

        protected override void RegisterServices(IServiceCollection services)
        {
            Interlocked.Increment(ref ServiceRuns);

            BsonClassMap.TryRegisterClassMap<SheetCoat>();
            BsonClassMap.TryRegisterClassMap<MattCoat>();
        }
    }

    /// <summary>One of two entities that claim one id.</summary>
    [OxQLType("mint.coin", "mint.coin")]
    public class CoinA : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>The other of two entities that claim one id.</summary>
    [OxQLType("mint.coin", "mint.coin")]
    public class CoinB : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>A service a strict host refuses to start, and one that reads its configuration and environment.</summary>
    public class MintStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>The environment and the setting the last instance was created with.</summary>
        public static string? SeenEnvironment, SeenSetting;

        private readonly string environmentName = SeenEnvironment = environment.EnvironmentName;
        private readonly string? setting = SeenSetting = configuration["Mint:Setting"];

        protected override string ServiceName => "Mint";

        protected override Type[] EntityTypes => [typeof(CoinA), typeof(CoinB)];
    }

    /// <summary>A service whose class-map hook throws.</summary>
    public class BrokenHookStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>What the hook throws.</summary>
        public const string Fault = "The class maps of the broken service cannot be registered.";

        /// <summary>How often the hook ran in this process.</summary>
        public static int HookRuns;

        protected override string ServiceName => "Broken";

        protected override Type[] EntityTypes => [];

        protected override void RegisterBsonClassMaps()
        {
            Interlocked.Increment(ref HookRuns);

            throw new InvalidOperationException(Fault);
        }
    }

    /// <summary>A startup without the constructor a host creates one through.</summary>
    public class OddStartup() : HostingStartup(new ConfigurationBuilder().Build(), null!)
    {
        protected override string ServiceName => "Odd";

        protected override Type[] EntityTypes => [];
    }
}
