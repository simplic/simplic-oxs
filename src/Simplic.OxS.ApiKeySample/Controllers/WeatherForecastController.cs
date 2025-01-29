using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Extensions;

namespace Simplic.OxS.ApiKeySample.Controllers
{
    [ApiController]
    [ApiKeyOrJwtAuthorize]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly IRequestContext requestContext;
        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IRequestContext requestContext)
        {
            _logger = logger;
            this.requestContext = requestContext;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public Response Get()
        {
            return
                new Response
                {
                    OId = requestContext.OrganizationId ?? Guid.Empty,
                    UId = requestContext.OrganizationId ?? Guid.Empty,
                };
         
        }
    }
}
