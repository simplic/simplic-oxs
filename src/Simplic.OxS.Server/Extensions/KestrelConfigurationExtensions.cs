using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Simplic.OxS.Server.Extensions;

/// <summary>
/// Extension methods for configuring Kestrel server
/// </summary>
public static class KestrelConfigurationExtensions
{
    /// <summary>
    /// Configures Kestrel to listen on specific ports for HTTP and HTTPS with gRPC support
    /// </summary>
    /// <param name="webBuilder">The web host builder to configure</param>
    /// <param name="httpPort">Port for HTTP connections (default: 8080)</param>
    /// <param name="httpsPort">Port for HTTPS connections with gRPC support (default: 8082)</param>
    /// <returns>The configured web host builder</returns>
    public static IWebHostBuilder ConfigureKestrelPorts(this IWebHostBuilder webBuilder, int httpPort = 8080, int httpsPort = 8082)
    {
        return webBuilder.ConfigureKestrel(options =>
        {
            // HTTP port for REST API and other endpoints
            options.ListenAnyIP(httpPort);
            
            // HTTPS port for gRPC and secure REST API
            options.ListenAnyIP(httpsPort, listenOptions =>
            {
                // listenOptions.UseHttps();
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });
    }
}