using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson.Serialization;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>An entity with a polymorphic collection.</summary>
    [OxQLType("depot.yard", "depot.yard")]
    public class Yard : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public IList<YardSlot> Slots { get; set; } = [];
    }

    /// <summary>The base of the stored slot variants.</summary>
    public abstract class YardSlot
    {
        public Guid Id { get; set; }
    }

    /// <summary>A stored slot variant.</summary>
    public class CoveredSlot : YardSlot
    {
        public int Height { get; set; }
    }

    /// <summary>A stored slot variant.</summary>
    public class OpenSlot : YardSlot
    {
        public bool Paved { get; set; }
    }

    /// <summary>A service that registers its class maps in the hook, without a guard of its own.</summary>
    public class DepotStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>How often the hook ran in this process.</summary>
        public static int HookRuns;

        protected override string ServiceName => "Depot";

        protected override Type[] EntityTypes => [typeof(Yard), typeof(YardSlot), typeof(CoveredSlot), typeof(OpenSlot)];

        protected override void RegisterBsonClassMaps()
        {
            Interlocked.Increment(ref HookRuns);

            BsonClassMap.RegisterClassMap<YardSlot>();
            BsonClassMap.RegisterClassMap<CoveredSlot>();
            BsonClassMap.RegisterClassMap<OpenSlot>();
        }
    }
}
