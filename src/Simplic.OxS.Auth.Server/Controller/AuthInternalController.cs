using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Settings;
using Simplix.OxS.Mail.SchemaRegistry;
using Simplix.OxS.Sms.SchemaRegistry;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Simplic.OxS.Auth.Server.Controller
{
    [ApiController]
    [Route("/internal/[controller]")]
    public class AuthInternalController : OxSInternalController
    {
        private readonly ILogger<AuthController> logger;
        private readonly IUserService userService;
        private readonly ITwoFactorTokenService twoFactorTokenService;
        private readonly IOptions<AuthSettings> authSettings;
        private readonly IBusControl busControl;

        public AuthInternalController(ILogger<AuthController> logger, IUserService userService, ITwoFactorTokenService twoFactorTokenService, IOptions<AuthSettings> authSettings, IBusControl busControl)
        {
            this.logger = logger;
            this.userService = userService;
            this.twoFactorTokenService = twoFactorTokenService;
            this.authSettings = authSettings;
            this.busControl = busControl;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(Model.RegisterResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Register([NotNull] Model.RegisterRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var user = await userService.GetAsync(model.EMail);

            if (user != null)
                return BadRequest(new { error = $"User already existss: {model.EMail}" });

            user = await userService.RegisterAsync(model.EMail, model.Password, model.PhoneNumber);

            // Enable user
            user.MailVerified = true;

            // Set user as registered
            await userService.UpdateAsync(user);

            // Publish user created
            await busControl.Publish<SchemaRegistry.CreateUserEvent>(user);

            var response = new Model.RegisterResponse
            {
                EMail = user.EMail
            };

            return Ok(response);
        }
    }
}
