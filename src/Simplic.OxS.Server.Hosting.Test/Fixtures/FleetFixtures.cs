using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson.Serialization;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>An entity with a polymorphic member.</summary>
    [OxQLType("fleet.unit", "fleet.unit")]
    public class Unit : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public UnitBody? Body { get; set; }
    }

    /// <summary>The base of the stored body variants.</summary>
    public abstract class UnitBody
    {
        public string? Label { get; set; }
    }

    /// <summary>A stored body variant.</summary>
    public class TankBody : UnitBody
    {
        public int Litres { get; set; }
    }

    /// <summary>A stored body variant.</summary>
    public class BoxBody : UnitBody
    {
        public int Pallets { get; set; }
    }

    /// <summary>The registrations of the service, guarded to run once per process.</summary>
    public static class FleetClassMaps
    {
        private static readonly object Gate = new();
        private static bool registered;

        /// <summary>How often the registrations really ran.</summary>
        public static int Runs;

        public static void Register()
        {
            lock (Gate)
            {
                if (registered)
                    return;

                Runs++;
                BsonClassMap.RegisterClassMap<UnitBody>();
                BsonClassMap.RegisterClassMap<TankBody>();
                BsonClassMap.RegisterClassMap<BoxBody>();
                registered = true;
            }
        }
    }

    /// <summary>
    /// A service that calls its guarded registration from <c>ConfigureOxSchema</c> and again from
    /// its mapper profiles.
    /// </summary>
    public class FleetStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Fleet";

        protected override Type[] EntityTypes => [typeof(Unit), typeof(UnitBody), typeof(TankBody), typeof(BoxBody)];

        protected override void ConfigureOxSchema(OxSchemaOptionsBuilder schema)
        {
            FleetClassMaps.Register();

            base.ConfigureOxSchema(schema);
        }

        protected override void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration)
        {
            FleetClassMaps.Register();

            base.RegisterMapperProfiles(mapperConfiguration);
        }
    }

    /// <summary>An entity of a service whose mapper profiles are faulty.</summary>
    [OxQLType("faulty.note", "faulty.note")]
    public class FaultyNote : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>A service whose mapper profiles throw.</summary>
    public class FaultyStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>What the profiles throw.</summary>
        public const string Fault = "The profile of the faulty service cannot be registered.";

        protected override string ServiceName => "Faulty";

        protected override Type[] EntityTypes => [typeof(FaultyNote)];

        protected override void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration) =>
            throw new InvalidOperationException(Fault);
    }
}
