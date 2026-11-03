using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OxQL.AspNetCore.Controllers;
using Simplic.OxS.Server.Controller;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>Models of the stock service, one of them named like a model of the schema document.</summary>
    public static class Stock
    {
        /// <summary>A property of a stock item, as the stock service answers it.</summary>
        public class OxSchemaProperty
        {
            public string Name { get; set; } = "";

            public int Shelf { get; set; }
        }
    }

    /// <summary>Models of the shelf service, one of them named like a model of the schema document and given an id of its own.</summary>
    public static class Shelf
    {
        /// <summary>A kind of shelf, as the shelf service answers it.</summary>
        [SwaggerSchemaId("ShelfType")]
        public class OxSchemaType
        {
            public string Label { get; set; } = "";

            public int Levels { get; set; }
        }
    }

    /// <summary>A controller that answers the service's own model.</summary>
    [ApiController]
    [Route("[controller]")]
    public class ShelfController : OxSController
    {
        /// <summary>The kinds of shelf.</summary>
        [HttpGet("types")]
        public ActionResult<IReadOnlyList<Shelf.OxSchemaType>> Types(CancellationToken ct) => Ok(Array.Empty<Shelf.OxSchemaType>());
    }

    /// <summary>
    /// A service with a model named like one of the schema document, which names its own model
    /// with the package's attribute and switches the attribute on.
    /// </summary>
    public class ShelfStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Shelf";

        protected override Type[] EntityTypes => [];

        protected override Type[] Controllers => [typeof(ShelfController), typeof(OxQLController), typeof(SchemaController)];

        protected override void RegisterServices(IServiceCollection services) => services.UseCustomSwaggerId();
    }

    /// <summary>A controller that answers the service's own model.</summary>
    [ApiController]
    [Route("[controller]")]
    public class StockController : OxSController
    {
        /// <summary>The properties of a stock item.</summary>
        [HttpGet("{id}/properties")]
        public ActionResult<IReadOnlyList<Stock.OxSchemaProperty>> Properties(Guid id, CancellationToken ct) => Ok(Array.Empty<Stock.OxSchemaProperty>());
    }

    /// <summary>
    /// A service with a model named like one of the schema document, which gives its own model
    /// another schema id and leaves every other id to the selector that was in place.
    /// </summary>
    public class StockStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Stock";

        protected override Type[] EntityTypes => [];

        protected override Type[] Controllers => [typeof(StockController), typeof(OxQLController), typeof(SchemaController)];

        protected override void RegisterServices(IServiceCollection services) =>
            services.Configure<SwaggerGenOptions>(options =>
            {
                var selector = options.SchemaGeneratorOptions.SchemaIdSelector;

                options.CustomSchemaIds(type => type == typeof(Stock.OxSchemaProperty) ? "StockProperty" : selector(type));
            });
    }
}
