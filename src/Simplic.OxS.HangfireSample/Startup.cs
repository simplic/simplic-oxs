using Hangfire;
using Simplic.OxS.Scheduler;
using Simplic.OxS.Server;

namespace Simplic.OxS.HangfireSample
{
    public class Startup : Bootstrap
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment currentEnvironment) : base(configuration, currentEnvironment)
        {
        }

        protected override void RegisterServices(IServiceCollection services)
        {
            services.AddJobScheduler(Configuration, ServiceName);

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

            // Every 5 minutes
            RecurringJob.AddOrUpdate("SampleCronJob", () => Console.WriteLine("Recurring!"), "*/5 * * * *");
        }

        protected override void MapEndpoints(IEndpointRouteBuilder builder)
        {
            builder.MapScheduler(ServiceName);
        }

        private readonly string _policyName = "CorsPolicy";

        protected override string ServiceName => "Sample";
    }
}
