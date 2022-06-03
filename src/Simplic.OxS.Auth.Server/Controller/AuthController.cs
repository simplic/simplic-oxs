using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Settings;
using Simplic.OxS.Mail.SchemaRegistry;
using Simplic.OxS.Sms.SchemaRegistry;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Simplic.OxS.Auth.Server.Controller
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : OxSController
    {
        private readonly ILogger<AuthController> logger;
        private readonly IUserService userService;
        private readonly ITwoFactorTokenService twoFactorTokenService;
        private readonly IOptions<AuthSettings> authSettings;
        private readonly IBusControl busControl;
        private readonly IRequestContext requestContext;

        public AuthController(IRequestContext requestContext, ILogger<AuthController> logger, IUserService userService, ITwoFactorTokenService twoFactorTokenService, IOptions<AuthSettings> authSettings, IBusControl busControl)
        {
            this.logger = logger;
            this.userService = userService;
            this.twoFactorTokenService = twoFactorTokenService;
            this.authSettings = authSettings;
            this.busControl = busControl;
            this.requestContext = requestContext;
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(Model.LoginResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Login([NotNull] Model.LoginRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var user = await userService.LoginAsync(model.EMail, model.Password);

            if (user == null)
                return StatusCode((int)HttpStatusCode.Unauthorized, new { error = "User not found" });

            if (!user.MailVerified)
            {
                return Ok(new Model.LoginResponse
                {
                    ErrorState = "mail_not_verified"
                });
            }

            var tokenHandler = new JwtSecurityTokenHandler();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Email, user.EMail),

                    // Default tenant
                    new Claim("TId", ""),

                    // Unique user id
                    new Claim("Id", $"{user.Id}")
                }),

                Expires = DateTime.Now.AddHours(24),

                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(authSettings.Value.Token)), SecurityAlgorithms.HmacSha256Signature)
            };

            foreach (var role in user.Roles)
                tokenDescriptor.Subject.AddClaim(new Claim(ClaimTypes.Role, role));

            var jwt = tokenHandler.CreateToken(tokenDescriptor) as JwtSecurityToken;

            // Create token
            var token = await twoFactorTokenService.CreateAsync(new Dictionary<string, string>
            {
                { "bearer", jwt.RawData },
                { "device", model.LoginDevice },
                { "userId", $"{user.Id}" }
            }, "login");

            var responseToken = $"{token.Id}";
            var tokenType = "two-factor";

            if (user.LoginDevice == model.LoginDevice)
            {
                responseToken = jwt.RawData;
                tokenType = "jwt";
            }
            else
            {
                await busControl.Send<SendSmsCommand>(new
                {
                    PhoneNumber = user.PhoneNumber,
                    TemplateId = "verification_sms",
                    Parameter = new Dictionary<string, object>
                    {
                        { "Code", token.Code }
                    }
                });
            }

            var response = new Model.LoginResponse
            {
                Token = responseToken,
                TokenType = tokenType
            };

            return Ok(response);
        }

        [HttpPost("select-tenant")]
        public async Task<IActionResult> SelectTenant()
        {
            return Ok();
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

            // Send mail and create verification token
            await busControl.Send<SendMailCommand>(new
            {
                MailAddress = user.EMail,
                TemplateId = 19,
                Parameter = new Dictionary<string, object>
                {
                    { "Code", user.MailVerificationCode }
                }
            });

            // Add welcome news entry
            // await busControl.Send<SchemaRegistry.AddNewsEntryCommand>(new
            // {
            //     UserId = user.Id,
            //     Subject = "Welcome",
            //     Message = $"Welcome {user.EMail}"
            // });

            // Publish user created
            await busControl.Publish<SchemaRegistry.CreateUserEvent>(user);

            var response = new Model.RegisterResponse
            {
                EMail = user.EMail
            };

            return Ok(response);
        }

        [HttpPost("send-verification-code")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> SendVerificationCode([NotNull] Model.SendVerificationCodeRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var user = await userService.GetAsync(model.EMailAddress);

            if (user != null)
            {
                // Send mail and create verification token
                await busControl.Send<SendMailCommand>(new
                {
                    MailAddress = user.EMail,
                    TemplateId = 19,
                    Parameter = new Dictionary<string, object>
                    {
                        { "Code", user.MailVerificationCode }
                    }
                });
            }

            return Ok();
        }

        [HttpPost("verify-mail")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> VerifyMail([NotNull] Model.VerifyMailRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var user = await userService.GetAsync(model.EMail);

            if (user != null)
            {
                if (!user.MailVerified && user.MailVerificationCode == model.Code)
                {
                    await userService.SetMailVerifiedAsync(user.Id);

                    return Ok();
                }
                else
                {
                    return BadRequest();
                }
            }

            return BadRequest();
        }

        [HttpPost("restore-password")]
        [ProducesResponseType(typeof(Model.ResetPasswordResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> ResetPassword([NotNull] Model.ResetPasswordRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var user = await userService.GetAsync(model.EMail);

            if (user != null)
            {
                var passwordHash = userService.GetPasswordHash(user, model.NewPassword);

                var token = await twoFactorTokenService.CreateAsync(new Dictionary<string, string>
                {
                    { "password", passwordHash },
                    { "userId", $"{user.Id}" }
                }, "change_password");

                await busControl.Send<SendSmsCommand>(new
                {
                    PhoneNumber = user.PhoneNumber,
                    TemplateId = "verification_sms",
                    Parameter = new Dictionary<string, object> { { "Code", token.Code } }
                });

                // Reset device, so that a verification is required with the next login
                await userService.SetLoginDevice(user.Id, $"{Guid.NewGuid()}");

                return Ok(new Model.ResetPasswordResponse
                {
                    TokenId = token.Id
                });
            }

            return BadRequest();
        }

        [HttpPost("change-password")]
        [ProducesResponseType(typeof(Model.ChangePasswordResponse), (int)HttpStatusCode.OK)]
        [Authorize]
        public async Task<IActionResult> ChangePassword([NotNull] Model.ChangePasswordRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var userId = requestContext.UserId;

            if (userId == null)
                return BadRequest();

            var user = await userService.GetAsync(userId.Value);

            if (user == null)
                return BadRequest(new { error = "User not exists" });

            var passwordHash = userService.GetPasswordHash(user, model.NewPassword);

            var token = await twoFactorTokenService.CreateAsync(new Dictionary<string, string>
            {
                { "password", passwordHash },
                { "userId", $"{userId}" }
            }, "change_password");

            await busControl.Send<SendSmsCommand>(new
            {
                PhoneNumber = user.PhoneNumber,
                TemplateId = "verification_sms",
                Parameter = new Dictionary<string, object> { { "Code", token.Code } }
            });

            return Ok(new Model.ChangePasswordResponse
            {
                TokenId = token.Id
            });
        }

        [HttpPost("verify-two-factor")]
        [ProducesResponseType(typeof(Model.TwoFactorResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> VerifyTwoFactor([NotNull] Model.TwoFactorRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var token = await twoFactorTokenService.GetAsync(model.TokenId);

            if (token?.Code == model.Code)
            {
                // Remove user token
                await twoFactorTokenService.DeleteAsync(token.Id);

                // Check whether we have some special tokens
                if (token.Action == "change_password")
                {
                    if (token.Payload.TryGetValue("password", out string password) && token.Payload.TryGetValue("userId", out string userId))
                    {
                        var user = await userService.GetAsync(Guid.Parse(userId));
                        if (user != null)
                        {
                            await userService.ChangePasswordAsync(user, password);

                            // Clear payload, since we don't want to give anything to the client here
                            token.Payload.Clear();
                        }
                        else
                        {
                            return BadRequest(new { error = "User not found for changing the password." });
                        }
                    }
                }
                if (token.Action == "login")
                {
                    if (token.Payload.TryGetValue("device", out string device) && token.Payload.TryGetValue("userId", out string userId))
                    {
                        await userService.SetLoginDevice(Guid.Parse(userId), device);
                    }
                }

                return Ok(new Model.TwoFactorResponse
                {
                    Payload = token.Payload
                });
            }

            return BadRequest(new { error = "Invalid token-id or code" });
        }
    }
}
