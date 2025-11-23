namespace Simplic.OxS.McpSample;

/// <summary>
/// Program entry point for MCP sample service
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point
    /// </summary>
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

/// <summary>
    /// Creates the host builder
    /// </summary>
public static IHostBuilder CreateHostBuilder(string[] args) =>
   Host.CreateDefaultBuilder(args)
     .ConfigureWebHostDefaults(webBuilder =>
{
   webBuilder.UseStartup<Startup>();
    });
}