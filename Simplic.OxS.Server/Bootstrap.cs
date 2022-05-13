using AutoMapper;
using Simplic.OxS.Data.MongoDB;
using Simplic.OxS.MessageBroker;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Simplic.OxS.Server
{
    public abstract class Bootstrap
    {
        public Bootstrap(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.Configure<AuthSettings>(options => Configuration.GetSection("Auth").Bind(options));
            services.Configure<ConnectionSettings>(options => Configuration.GetSection("MongoDB").Bind(options));

            // Setup database stuff
            var databaseSection = Configuration.GetSection("MongoDB");
            if (databaseSection != null)
            {
                Console.WriteLine(" > Add MongoDB context");
                services.AddTransient<IMongoContext, MongoContext>();
            }
            else
            {
                Console.WriteLine(" > NO MongoDB context found.");
            }

            // Initialize broker system
            var rabbitMQSettings = Configuration.GetSection("rabbitMQ").Get<MessageBrokerSettings>();
            if (rabbitMQSettings != null && !string.IsNullOrWhiteSpace(rabbitMQSettings.Host))
            {
                Console.WriteLine($" > Add MassTransit context: {rabbitMQSettings.Host}@{rabbitMQSettings.UserName}");
                services.InitializeMassTransit(Configuration, null);

                ConfigureEndpointConventions(services, rabbitMQSettings);
            }
            else
            {
                Console.WriteLine(" > NO MassTransit context found.");
            }

            var authSettings = Configuration.GetSection("Auth").Get<AuthSettings>();

            if (authSettings != null)
            {
                services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer(x =>
                {
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;

                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(authSettings.Token)),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });
            }

            RegisterServices(services);

            // Create mapper profiles and register mapper
            var mapperConfig = new MapperConfiguration(RegisterMapperProfiles);

            IMapper mapper = mapperConfig.CreateMapper();
            services.AddSingleton(mapper);

            // Register web-api controller. Must be executed before creating swagger configuration
            services.AddControllers();

            // Add swagger stuff
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc(ApiVersion, new OpenApiInfo { Title = $"Simplic.OxS{ServiceName}", Version = ApiVersion });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header,

                        },
                        new List<string>()
                    }
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment() || env.EnvironmentName.ToLower() == "local")
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint($"/swagger/{ApiVersion}/swagger.json", $"Simplic.OxS{ServiceName} {ApiVersion}"));

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            var migrationService = app.ApplicationServices.GetService<IDatabaseMigrationService>();
            migrationService?.Migrate().Wait();
        }

        protected virtual void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration)
        { 
            
        }

        protected abstract void ConfigureEndpointConventions(IServiceCollection services, MessageBrokerSettings settings);

        protected abstract void RegisterServices(IServiceCollection services);

        protected abstract string ServiceName { get; }

        protected virtual string ApiVersion { get; } = "v1";

        public IConfiguration Configuration { get; }
    }
}
