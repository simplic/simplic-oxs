using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Simplic.OxS.Identity.Extension;
using System.Text;
using Simplic.OxS.Server.Handler;

namespace Simplic.OxS.Server.Extensions;

/// <summary>
/// Extension method for adding authorization and authentication 
/// </summary>
internal static class AuthenticationExtension
{
    /// <summary>
    /// Add authentication
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Service configuration</param>
    /// <returns>Auth builder instance</returns>
    internal static AuthenticationBuilder? AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OxS.Settings.AuthSettings>(options => configuration.GetSection("Auth").Bind(options));
        var authSettings = configuration.GetSection("Auth").Get<OxS.Settings.AuthSettings>();

        if (authSettings != null)
        {
            services.UseApiKeyValidation();
            return services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = "MultipleAuth";
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme("MultipleAuth", "Multiple Auth Schemes", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey("Authorization")) return JwtBearerDefaults.AuthenticationScheme;
                    if (context.Request.Headers.ContainsKey(Constants.HttpHeaderApiKey)) return "ApiKey";
                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(x =>
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
