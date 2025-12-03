using Simplic.OxS.GrpcSample.Server.Services;
using Simplic.OxS.Server;

namespace Simplic.OxS.GrpcSample.Server;

/// <summary>
/// Bootstrap configuration for the gRPC sample server
/// </summary>
public class Startup : Bootstrap
{
    public Startup(IConfiguration configuration, IWebHostEnvironment currentEnvironment) 
        : base(configuration, currentEnvironment)
    {
    }

    /// <summary>
    /// Register custom services for the gRPC sample
    /// </summary>
    protected override void RegisterServices(IServiceCollection services)
    {
        // Register the gRPC UserService
        services.AddScoped<UserService>();
        
        // Add CORS for development/testing
        services.AddCors(options =>
        {
            options.AddPolicy(name: "AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyHeader()
                       .AllowAnyMethod();
            });
        });
    }

    /// <summary>
    /// Map gRPC endpoints
    /// </summary>
    protected override void MapGrpcEndpoints(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder builder)
    {
        // Map the UserService gRPC endpoint
        builder.MapGrpcService<UserService>();
        
        Console.WriteLine("gRPC UserService mapped successfully");
    }

    /// <summary>
    /// Configure application pipeline
    /// </summary>
    public override void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Call base configuration first
        base.Configure(app, env);
        
        // Add CORS
        app.UseCors("AllowAll");
        
        Console.WriteLine("gRPC Sample Server configured successfully");
    }

    /// <summary>
    /// Service name for the gRPC sample
    /// </summary>
    protected override string ServiceName => "GrpcSample";
}