using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>A service that notes the order its hooks run in, a hosted service of its own and the schema build among them.</summary>
    public class OrderedStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>What ran, in the order it ran.</summary>
        public static readonly List<string> Steps = [];

        protected override string ServiceName => "Ordered";

        protected override Type[] EntityTypes => [];

        protected override void RegisterBsonClassMaps() => Steps.Add(nameof(RegisterBsonClassMaps));

        protected override void RegisterServices(IServiceCollection services)
        {
            Steps.Add(nameof(RegisterServices));

            services.AddHostedService<Early>();
            services.AddTransient<IStartupFilter, AfterTheSchema>();
        }

        protected override void ConfigureOxSchema(OxSchemaOptionsBuilder schema)
        {
            base.ConfigureOxSchema(schema);
            Steps.Add(nameof(ConfigureOxSchema));
        }

        protected override void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration) => Steps.Add(nameof(RegisterMapperProfiles));

        private sealed class Early : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken)
            {
                Steps.Add("a hosted service starts");

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        /// <summary>Runs where the host builds its pipeline: the registry it finds there is built.</summary>
        private sealed class AfterTheSchema(IServiceProvider provider) : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                provider.GetRequiredService<OxSchemaRegistry>();
                Steps.Add("the schema is built");
                next(app);
            };
        }
    }

    /// <summary>A service whose mapper profiles throw, and count how often they ran.</summary>
    public class BrokenProfilesStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>What the profiles throw.</summary>
        public const string Fault = "The profiles of the broken service cannot be registered.";

        /// <summary>How often the profiles ran in this process.</summary>
        public static int ProfileRuns;

        protected override string ServiceName => "Brokenprofiles";

        protected override Type[] EntityTypes => [];

        protected override void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration)
        {
            Interlocked.Increment(ref ProfileRuns);

            throw new InvalidOperationException(Fault);
        }
    }

    /// <summary>
    /// A service whose first run of the mapper profiles stays inside them until a test lets it
    /// go, and which counts how many runs were inside at once.
    /// </summary>
    public class GatedProfilesStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        private static int inside;

        /// <summary>Set when a run is inside the profiles.</summary>
        public static readonly ManualResetEventSlim Entered = new();

        /// <summary>Set when a second run is inside the profiles.</summary>
        public static readonly ManualResetEventSlim SecondEntered = new();

        /// <summary>What the first run waits for.</summary>
        public static readonly ManualResetEventSlim Gate = new();

        /// <summary>How often the profiles ran in this process.</summary>
        public static int ProfileRuns;

        /// <summary>The most runs that were inside the profiles at the same time.</summary>
        public static int MostAtOnce;

        protected override string ServiceName => "Gated";

        protected override Type[] EntityTypes => [];

        protected override void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration)
        {
            var run = Interlocked.Increment(ref ProfileRuns);

            var now = Interlocked.Increment(ref inside);

            lock (Gate)
                MostAtOnce = Math.Max(MostAtOnce, now);

            Entered.Set();

            if (run >= 2)
                SecondEntered.Set();
            Gate.Wait(TimeSpan.FromSeconds(30));

            Interlocked.Decrement(ref inside);
        }
    }
}
