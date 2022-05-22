using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simplic.OxS.Sms.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SmsController : ControllerBase
    {
        [HttpGet]
        public IActionResult Ping()
        {
            return Ok(new { pong = "Sms service is available." });
        }
    }
}
