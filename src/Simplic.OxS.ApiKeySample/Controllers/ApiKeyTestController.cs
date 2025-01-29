using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Extensions;

namespace Simplic.OxS.ApiKeySample.Controllers
{
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class ApiKeyTestController : OxSController
    {
        
        private readonly IRequestContext requestContext;
        private readonly ILogger<ApiKeyTestController> _logger;

        public ApiKeyTestController(ILogger<ApiKeyTestController> logger, IRequestContext requestContext)
        {
            _logger = logger;
            this.requestContext = requestContext;
        }

        [HttpGet(Name = "TestApiKey")]
        public IActionResult Get()
        {
            return Ok(
                new Response
                {
                    OId = requestContext.OrganizationId ?? Guid.Empty,
                    UId = requestContext.UserId ?? Guid.Empty,
                });
         
        }
    }
}
