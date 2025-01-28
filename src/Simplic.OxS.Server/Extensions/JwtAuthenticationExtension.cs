using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Simplic.OxS.Server.Implementations;
using Simplic.OxS.Server.Interface;
using Simplic.OxS.Server.Middleware;
using Simplic.OxS.Server.Settings;
using System.Text;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// Extension method for adding Jwt authorization and authentication
    /// </summary>
    internal static class JwtAuthenticationExtension
    {
        /// <summary>
        /// Add jwt authentication
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Service configuration</param>
        /// <returns>Auth builder instance</returns>
        internal static AuthenticationBuilder? AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<OxS.Settings.AuthSettings>(options => configuration.GetSection("Auth").Bind(options));
            var authSettings = configuration.GetSection("Auth").Get<OxS.Settings.AuthSettings>();

            if (authSettings != null)
            {
                services.AddSingleton<IApiKeyValidator, ApiKeyValidator>(); // Register your API key validator service

                return services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer(x =>
                {
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;

                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(authSettings.Token)),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };

                    x.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                }).AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options => { }); 
            }

            return null;
        }
    }
}
