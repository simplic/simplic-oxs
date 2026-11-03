using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OxQL.AspNetCore.Controllers;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.OxQL;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>An entity of the parcel service.</summary>
    [OxQLType("parcel.parcel", "parcel.parcel")]
    public class Parcel : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public string Label { get; set; } = "";

        public decimal WeightKg { get; set; }
    }

    /// <summary>What a caller sends to register a parcel.</summary>
    public class ParcelRequest
    {
        public string Label { get; set; } = "";

        public decimal WeightKg { get; set; }
    }

    /// <summary>A parcel as the service answers it.</summary>
    public class ParcelResponse
    {
        public Guid Id { get; set; }

        public string Label { get; set; } = "";

        public decimal WeightKg { get; set; }

        public ParcelState State { get; set; }
    }

    /// <summary>Where a parcel is.</summary>
    public enum ParcelState
    {
        Registered,
        Underway,
        Delivered,
    }

    /// <summary>A controller as a service writes one: nothing of the query or schema routes.</summary>
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class ParcelController : OxSController
    {
        /// <summary>One parcel.</summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ParcelResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Get(Guid id, CancellationToken ct) => NotFound();

        /// <summary>The parcels of a state.</summary>
        [HttpGet]
        public ActionResult<IReadOnlyList<ParcelResponse>> List([FromQuery] ParcelState? state, [FromQuery] int limit = 50, CancellationToken ct = default) => Ok(Array.Empty<ParcelResponse>());

        /// <summary>Registers a parcel.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ParcelResponse), StatusCodes.Status201Created)]
        public IActionResult Post([FromBody] ParcelRequest request, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, new ParcelResponse { Label = request.Label });

        /// <summary>Whether the service takes parcels today.</summary>
        [HttpGet("open")]
        [AllowAnonymous]
        public ActionResult<bool> Open(CancellationToken ct) => true;
    }

    /// <summary>A service with a controller of its own beside the query and schema routes of the package.</summary>
    public class ParcelStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Parcel";

        protected override Type[] EntityTypes => [typeof(Parcel)];

        protected override Type[] Controllers => [typeof(ParcelController), typeof(OxQLController), typeof(SchemaController), typeof(OxQLInternalController)];
    }
}
