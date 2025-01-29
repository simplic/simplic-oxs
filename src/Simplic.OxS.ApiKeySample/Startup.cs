using Simplic.OxS.Server;

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

        protected override string ServiceName => "Sample";
    }
}
