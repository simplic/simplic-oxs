using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>A service that declares no entity, on purpose.</summary>
    public class NoticeStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Notice";

        protected override Type[] EntityTypes => [];
    }

    /// <summary><see cref="NoticeStartup"/> with the query routes mapped.</summary>
    public class NoticeQueryStartup(IConfiguration configuration, IWebHostEnvironment environment) : NoticeStartup(configuration, environment)
    {
        protected override Type[] Controllers => [typeof(global::OxQL.AspNetCore.Controllers.OxQLController)];
    }

    /// <summary><see cref="SalvageStartup"/> with the query routes mapped.</summary>
    public class SalvageQueryStartup(IConfiguration configuration, IWebHostEnvironment environment) : SalvageStartup(configuration, environment)
    {
        protected override Type[] Controllers => [typeof(global::OxQL.AspNetCore.Controllers.OxQLController)];
    }

    /// <summary>A service whose entity assembly cannot be read, so its schema build fails.</summary>
    public class SalvageStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Salvage";

        protected override Type[] EntityTypes => [];

        /// <inheritdoc/>
        protected override IList<Assembly> GetOxQLTypeAssemblies() => [new Unreadable()];

        private sealed class Unreadable : Assembly
        {
            public override Type[] GetTypes() => throw new InvalidOperationException("The types of this assembly cannot be read.");

            public override string FullName => "Salvage.Unreadable";
        }
    }
}
