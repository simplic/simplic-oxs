using Microsoft.AspNetCore.Mvc;

namespace Simplic.OxS.Mail.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MailController : ControllerBase
    {
        [HttpGet]
        public IActionResult Ping()
        {
            return Ok(new { pong = "Mail service is available." });
        }
    }
}
