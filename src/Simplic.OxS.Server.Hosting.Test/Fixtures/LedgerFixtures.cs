using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>An entity with a polymorphic collection by base class and one by interface.</summary>
    [OxQLType("ledger.booking", "ledger.booking")]
    public class Booking : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public string? Number { get; set; }

        public IList<BookingItem> Items { get; set; } = [];

        public IList<IBookingStep> Steps { get; set; } = [];
    }

    /// <summary>The base of the stored item variants.</summary>
    public abstract class BookingItem
    {
        public Guid Id { get; set; }

        public string? Text { get; set; }
    }

    /// <summary>A stored item variant.</summary>
    public class ArticleBookingItem : BookingItem
    {
        public decimal Quantity { get; set; }
    }

    /// <summary>A stored item variant.</summary>
    public class NoteBookingItem : BookingItem
    {
        public string? Note { get; set; }
    }

    /// <summary>A member typed by an interface: only a serializer registered for the interface reads it back.</summary>
    public interface IBookingStep
    {
        Guid Id { get; set; }
    }

    /// <summary>A stored implementation.</summary>
    public class ApproveStep : IBookingStep
    {
        public Guid Id { get; set; }

        public string? ApprovedBy { get; set; }
    }

    /// <summary>A stored implementation.</summary>
    public class RejectStep : IBookingStep
    {
        public Guid Id { get; set; }

        public string? Reason { get; set; }
    }

    /// <summary>The response model the mapper produces.</summary>
    public class BookingModel
    {
        public Guid Id { get; set; }

        public string? Number { get; set; }
    }

    /// <summary>The mapper profile of the service.</summary>
    public class BookingProfile : Profile
    {
        public BookingProfile() => CreateMap<Booking, BookingModel>();
    }

    /// <summary>A route that needs the mapper, as nearly every route of a service does.</summary>
    [ApiController]
    [AllowAnonymous]
    [Route("booking-probe")]
    public class BookingProbeController(IMapper mapper) : ControllerBase
    {
        [HttpGet]
        public BookingModel Get() => mapper.Map<BookingModel>(new Booking { Id = Guid.Empty, Number = "B-1" });
    }

    /// <summary>
    /// A service that registers its class maps and an interface serializer in its mapper
    /// profiles, without a guard: a second call, or a call after the schema froze the types,
    /// throws.
    /// </summary>
    public class LedgerStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>How often the profiles were registered in this process.</summary>
        public static int ProfileRuns;

        protected override string ServiceName => "Ledger";

        protected override Type[] EntityTypes =>
            [typeof(Booking), typeof(BookingItem), typeof(ArticleBookingItem), typeof(NoteBookingItem), typeof(IBookingStep), typeof(ApproveStep), typeof(RejectStep)];

        protected override Type[] Controllers => [typeof(BookingProbeController)];

        protected override void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration)
        {
            Interlocked.Increment(ref ProfileRuns);

            mapperConfiguration.AddProfile<BookingProfile>();

            BsonClassMap.RegisterClassMap<BookingItem>();
            BsonClassMap.RegisterClassMap<ArticleBookingItem>();
            BsonClassMap.RegisterClassMap<NoteBookingItem>();
            BsonClassMap.RegisterClassMap<ApproveStep>();
            BsonClassMap.RegisterClassMap<RejectStep>();
            BsonSerializer.RegisterSerializer(new DiscriminatedInterfaceSerializer<IBookingStep>());

            base.RegisterMapperProfiles(mapperConfiguration);
        }
    }
}
