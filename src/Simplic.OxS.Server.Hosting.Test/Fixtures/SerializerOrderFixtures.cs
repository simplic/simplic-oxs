using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    // The driver's serializers are the process's, so every startup below registers for a value
    // type of its own (Gauge<TMark> with its own mark): what one test registers or looks up
    // leaves the type of another untouched, in whatever order the tests run.

    /// <summary>A stored value with a serializer of its own; <typeparamref name="TMark"/> makes it one startup's.</summary>
    public readonly record struct Gauge<TMark>(int Value);

    /// <summary>A stored type with a member of the value type.</summary>
    public class Reading<TMark>
    {
        public Guid Id { get; set; }

        public Gauge<TMark> Gauge { get; set; }
    }

    /// <summary>A plain serializer: it keeps no other serializer.</summary>
    public sealed class GaugeSerializer<TMark> : StructSerializerBase<Gauge<TMark>>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Gauge<TMark> value) =>
            context.Writer.WriteInt32(value.Value);

        public override Gauge<TMark> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) =>
            new(context.Reader.ReadInt32());
    }

    /// <summary>A serializer that keeps the serializer of the value type, looked up when it is created.</summary>
    public sealed class ReadingListSerializer<TMark> : SerializerBase<List<Gauge<TMark>>>
    {
        /// <summary>The serializer of the value type as it was when this one was created.</summary>
        public IBsonSerializer<Gauge<TMark>> Kept { get; } = BsonSerializer.LookupSerializer<Gauge<TMark>>();
    }

    /// <summary>The registrations the startups below are made of.</summary>
    public static class SerializerOrder
    {
        /// <summary>A class map that asks for the serializer of its member: the first lookup of the value type's.</summary>
        public static void ClassMapThatLooksItsMemberUp<TMark>() =>
            BsonClassMap.RegisterClassMap<Reading<TMark>>(map =>
            {
                map.AutoMap();
                _ = map.GetMemberMap(reading => reading.Gauge).GetSerializer();
            });

        /// <summary>A serializer that keeps the value type's: the first lookup of it.</summary>
        public static void SerializerThatKeepsAnother<TMark>() =>
            BsonSerializer.RegisterSerializer(new ReadingListSerializer<TMark>());

        /// <summary>The value type's own serializer.</summary>
        public static void PlainSerializer<TMark>() =>
            BsonSerializer.RegisterSerializer(new GaugeSerializer<TMark>());
    }

    /// <summary>A startup without entities whose registrations a test names.</summary>
    public abstract class SerializerOrderStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override Type[] EntityTypes => [];
    }

    public sealed class MarkServices;

    /// <summary>The hook's class map looks the serializer up; <c>RegisterServices</c> registers it afterwards.</summary>
    public class LateInServicesStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        protected override string ServiceName => "Lateinservices";

        protected override void RegisterBsonClassMaps() => SerializerOrder.ClassMapThatLooksItsMemberUp<MarkServices>();

        protected override void RegisterServices(IServiceCollection services) => SerializerOrder.PlainSerializer<MarkServices>();
    }

    public sealed class MarkTried;

    /// <summary>The hook's class map looks the serializer up; <c>RegisterServices</c> tries to register it afterwards.</summary>
    public class TriedInServicesStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        protected override string ServiceName => "Triedinservices";

        protected override void RegisterBsonClassMaps() => SerializerOrder.ClassMapThatLooksItsMemberUp<MarkTried>();

        protected override void RegisterServices(IServiceCollection services) => BsonSerializer.TryRegisterSerializer(new GaugeSerializer<MarkTried>());
    }

    public sealed class MarkKeeper;

    /// <summary>The hook registers a serializer that keeps another; <c>RegisterServices</c> registers that other afterwards.</summary>
    public class LateAfterKeeperStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        protected override string ServiceName => "Lateafterkeeper";

        protected override void RegisterBsonClassMaps() => SerializerOrder.SerializerThatKeepsAnother<MarkKeeper>();

        protected override void RegisterServices(IServiceCollection services) => SerializerOrder.PlainSerializer<MarkKeeper>();
    }

    public sealed class MarkProfiles;

    /// <summary>The mapper profiles register the serializer after the hook's class map looked it up, inside what a profile's creation throws.</summary>
    public class LateInProfilesStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        protected override string ServiceName => "Lateinprofiles";

        protected override void RegisterBsonClassMaps() => SerializerOrder.ClassMapThatLooksItsMemberUp<MarkProfiles>();

        protected override void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration)
        {
            try
            {
                SerializerOrder.PlainSerializer<MarkProfiles>();
            }
            catch (Exception exception)
            {
                // What a profile created by reflection surfaces a fault of its constructor as.
                throw new System.Reflection.TargetInvocationException(exception);
            }
        }
    }

    public sealed class MarkSchema;

    /// <summary><c>ConfigureOxSchema</c> registers the serializer after the hook's class map looked it up.</summary>
    public class LateInSchemaStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        protected override string ServiceName => "Lateinschema";

        protected override void RegisterBsonClassMaps() => SerializerOrder.ClassMapThatLooksItsMemberUp<MarkSchema>();

        protected override void ConfigureOxSchema(OxSchemaOptionsBuilder schema)
        {
            base.ConfigureOxSchema(schema);
            SerializerOrder.PlainSerializer<MarkSchema>();
        }
    }

    public sealed class MarkHook;

    /// <summary>The hook itself registers the plain serializer after the one that keeps it.</summary>
    public class WrongOrderInHookStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        /// <summary>How often the hook ran in this process.</summary>
        public static int HookRuns;

        protected override string ServiceName => "Wrongorderinhook";

        protected override void RegisterBsonClassMaps()
        {
            Interlocked.Increment(ref HookRuns);

            SerializerOrder.SerializerThatKeepsAnother<MarkHook>();
            SerializerOrder.PlainSerializer<MarkHook>();
        }
    }

    public sealed class MarkOrdered;

    /// <summary>The hook in the order that works: the plain serializer, the class map, the serializer that keeps another.</summary>
    public class OrderedHookStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        protected override string ServiceName => "Orderedhook";

        protected override void RegisterBsonClassMaps()
        {
            SerializerOrder.PlainSerializer<MarkOrdered>();
            SerializerOrder.ClassMapThatLooksItsMemberUp<MarkOrdered>();
            SerializerOrder.SerializerThatKeepsAnother<MarkOrdered>();
        }
    }

    public sealed class MarkUnfrozen;

    /// <summary>The hook's class map looks nothing up, so a serializer of its member's type is still registered afterwards.</summary>
    public class UnfrozenClassMapStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        protected override string ServiceName => "Unfrozenclassmap";

        protected override void RegisterBsonClassMaps() => BsonClassMap.RegisterClassMap<Reading<MarkUnfrozen>>(map => map.AutoMap());

        protected override void RegisterServices(IServiceCollection services) => SerializerOrder.PlainSerializer<MarkUnfrozen>();
    }

    public sealed class MarkCheck;

    /// <summary>A service that registers a serializer in <c>RegisterServices</c> without a guard, and counts the runs.</summary>
    public class UnguardedInServicesStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        /// <summary>How often the service registration ran in this process.</summary>
        public static int ServiceRuns;

        protected override string ServiceName => "Unguardedinservices";

        protected override void RegisterServices(IServiceCollection services)
        {
            Interlocked.Increment(ref ServiceRuns);

            SerializerOrder.PlainSerializer<MarkCheck>();
        }
    }

    public sealed class MarkCheckLate;

    /// <summary>What <see cref="LateInServicesStartup"/> does, for the schema check.</summary>
    public class LateInServicesCheckedStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        /// <summary>How often the service registration ran in this process.</summary>
        public static int ServiceRuns;

        protected override string ServiceName => "Lateinserviceschecked";

        protected override void RegisterBsonClassMaps() => SerializerOrder.ClassMapThatLooksItsMemberUp<MarkCheckLate>();

        protected override void RegisterServices(IServiceCollection services)
        {
            Interlocked.Increment(ref ServiceRuns);

            SerializerOrder.PlainSerializer<MarkCheckLate>();
        }
    }

    public sealed class MarkHostThenCheck;

    /// <summary>A service a host is built from before its schema is checked.</summary>
    public class HostThenCheckStartup(IConfiguration configuration, IWebHostEnvironment environment) : SerializerOrderStartup(configuration, environment)
    {
        /// <summary>How often the service registration ran in this process.</summary>
        public static int ServiceRuns;

        protected override string ServiceName => "Hostthencheck";

        protected override void RegisterServices(IServiceCollection services)
        {
            Interlocked.Increment(ref ServiceRuns);

            SerializerOrder.PlainSerializer<MarkHostThenCheck>();
        }
    }
}
