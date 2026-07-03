using Simplic.OxS.Server;
using System.Reflection;

namespace Simplic.OxS.ApiKeySample
{
    public class Startup : Bootstrap
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment currentEnvironment) : base(configuration, currentEnvironment)
        {
        }

        protected override void RegisterServices(IServiceCollection services)
        {
            // Add job services
            

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

        public override void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            base.Configure(app, env);

        }

        protected override void MapEndpoints(IEndpointRouteBuilder builder)
        {
        }

        private readonly string _policyName = "CorsPolicy";

        /// <summary>
        /// Get the assemblies containing OxQL types for the gRPC sample
        /// </summary>
        /// <returns></returns>
        protected override IList<Assembly> GetOxQLTypeAssemblies() => [typeof(Startup).Assembly];

        protected override string ServiceName => "Sample";
    }
}
