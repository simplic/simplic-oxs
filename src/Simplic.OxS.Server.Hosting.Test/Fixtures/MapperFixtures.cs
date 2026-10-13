using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
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
            Interlocked.Increment(ref ProfileRuns);

            var now = Interlocked.Increment(ref inside);

            lock (Gate)
                MostAtOnce = Math.Max(MostAtOnce, now);

            Entered.Set();
            Gate.Wait(TimeSpan.FromSeconds(30));

            Interlocked.Decrement(ref inside);
        }
    }
}
