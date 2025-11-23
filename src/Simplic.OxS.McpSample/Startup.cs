using Simplic.OxS.Mcp.Server.Examples;
using Simplic.OxS.Mcp.Server.Extensions;
using Simplic.OxS.Server;

namespace Simplic.OxS.McpSample;

/// <summary>
/// Startup class for MCP sample service
/// </summary>
public class Startup : Bootstrap
{
    /// <summary>
    /// Initializes a new instance of the Startup class
    /// </summary>
    public Startup(IConfiguration configuration, IWebHostEnvironment currentEnvironment)
                    : base(configuration, currentEnvironment)
    {
    }

    /// <inheritdoc />
    protected override void RegisterServices(IServiceCollection services)
    {
        // Add CORS for development
        services.AddCors(opt =>
        {
            opt.AddPolicy(name: _policyName, builder =>
            {
                builder.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
        });
    }

    /// <inheritdoc />
    protected override void ConfigureMcpServices(IServiceCollection services)
    {
        // Register example MCP tools
        services.AddMcpTool<OrganizationInfoTool>();
        services.AddMcpTool<CalculatorTool>();

        // Alternative way to register multiple tools:
        // services.AddMcpTools(typeof(OrganizationInfoTool), typeof(CalculatorTool));
    }

    /// <inheritdoc />
    protected override bool IsMcpEnabled() => true;

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        base.Configure(app, env);

        // Configure CORS for development
        app.UseCors(_policyName);
    }

    /// <inheritdoc />
    protected override string ServiceName => "McpSample";

    private readonly string _policyName = "CorsPolicy";
}