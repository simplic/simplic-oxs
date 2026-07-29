using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Simplic.OxS.Server.Extensions;

/// <summary>
/// Shared CORS configuration for Simplic OxS services.
/// <para>
/// Replaces the hand-rolled <c>AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()</c> block that
/// is currently duplicated across the fleet.
/// </para>
/// </summary>
public static class CorsExtension
{
    /// <summary>
    /// Name of the policy registered by <see cref="AddDefaultCors"/>.
    /// </summary>
    public const string DefaultPolicyName = "OxSDefaultCorsPolicy";

    /// <summary>
    /// Registers the standard Simplic OxS CORS policy as the application default.
    /// </summary>
    /// <remarks>
    /// Reads <c>Cors:AllowedOrigins</c> from configuration (a string array).
    /// <list type="bullet">
    /// <item>
    /// When origins are configured, the policy allows exactly those and enables credentials —
    /// required for SignalR with authentication.
    /// </item>
    /// <item>
    /// When none are configured, the policy falls back to allowing any origin <b>without</b>
    /// credentials. <c>AllowAnyOrigin</c> and <c>AllowCredentials</c> are mutually exclusive in
    /// the CORS spec; combining them makes the browser reject the response, which silently breaks
    /// authenticated SignalR. This method therefore never emits that combination.
    /// </item>
    /// </list>
    /// Configure explicit origins in every environment that needs credentialed requests:
    /// <code>
    /// "Cors": { "AllowedOrigins": [ "https://app.simplic.biz" ] }
    /// </code>
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    public static IServiceCollection AddDefaultCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        return services.AddCors(options =>
        {
            options.AddPolicy(DefaultPolicyName, policy =>
            {
                policy.AllowAnyHeader().AllowAnyMethod();

                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins).AllowCredentials();
                else
                    policy.AllowAnyOrigin();
            });

            options.DefaultPolicyName = DefaultPolicyName;
        });
    }

    /// <summary>
    /// Applies the policy registered by <see cref="AddDefaultCors"/>.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>UseRouting</c> and before <c>UseAuthorization</c>.
    /// </remarks>
    /// <param name="app">Application builder.</param>
    public static IApplicationBuilder UseDefaultCors(this IApplicationBuilder app)
        => app.UseCors(DefaultPolicyName);
}
