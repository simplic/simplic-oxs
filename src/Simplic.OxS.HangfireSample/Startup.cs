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
            // Add job services
            services.AddTransient<SampleJob>();
            services.AddTransient<SampleAsyncJob>();

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

            // Every minute
            RecurringJob.AddOrUpdate<ScopedJob>("SampleJob", "sample", (x) => x.ExecuteJob(typeof(SampleJob), new ScopedJobParameter
            {
                OrganizationId = Guid.NewGuid(),
                UserId = Guid.Empty,
                Parameters = new Dictionary<string, string> 
                {
                    { "Key", "Value!" }
                }
            }), "* * * * *");


            // Every minute
            RecurringJob.AddOrUpdate<AsyncScopedJob>("SampleJobAsync", "sample", (x) => x.ExecuteJobAsync(typeof(SampleAsyncJob), new ScopedJobParameter
            {
                OrganizationId = Guid.NewGuid(),
                UserId = Guid.Empty,
                Parameters = new Dictionary<string, string>
                {
                    { "Key", "Value!" }
                }
            }), "* * * * *");
        }

        protected override void MapEndpoints(IEndpointRouteBuilder builder)
        {
            builder.MapScheduler(ServiceName);
        }

        private readonly string _policyName = "CorsPolicy";

        protected override string ServiceName => "Sample";
    }
}
