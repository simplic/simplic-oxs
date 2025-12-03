using Simplic.OxS.Server.Extensions;

namespace Simplic.OxS.GrpcSample.Server;

/// <summary>
/// Main program entry point for the gRPC sample server
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();

                // Configure Kestrel to listen on specific ports using extension method
                webBuilder.ConfigureKestrelPorts();
            });
}