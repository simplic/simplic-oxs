using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using OxQL.Core.Attributes;
using OxQL.Model.Attributes;
using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>An entity that references entities three other services own.</summary>
    [OxQLType("dispatch.job", "dispatch.job")]
    public class Job : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        [OxQLReference("carpool.car", "id")]
        public Guid CarId { get; set; }

        [OxQLReference("crew.member", "id")]
        public Guid MemberId { get; set; }

        [OxQLReference("billing.invoice", "id")]
        public Guid InvoiceId { get; set; }
    }

    /// <summary>A service whose entity references into other services.</summary>
    public class DispatchStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Dispatch";

        protected override Type[] EntityTypes => [typeof(Job)];
    }
}
