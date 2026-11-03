using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>An entity whose id carries no service prefix.</summary>
    [OxQLType("register_entry", "register_entry")]
    public class RegisterEntry : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>An entity whose id carries the prefix of another service.</summary>
    [OxQLType("archive.register_page", "register_page")]
    public class RegisterPage : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>A service whose entity ids another service cannot reference, on a host that is strict in every way.</summary>
    public class RegisterStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Register";

        protected override Type[] EntityTypes => [typeof(RegisterEntry), typeof(RegisterPage)];

        protected override void ConfigureOxSchema(OxSchemaOptionsBuilder schema) => schema.ContinuousIntegration = true;
    }
}
