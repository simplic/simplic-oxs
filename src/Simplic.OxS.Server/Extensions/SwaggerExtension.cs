using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Simplic.OxS.Server.Extensions
{
    internal static class SwaggerExtension

    {
        internal static IServiceCollection AddSwagger(this IServiceCollection services, IWebHostEnvironment env, string apiVersion, string serviceName)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc(apiVersion, new OpenApiInfo { Title = $"Simplic.OxS.{serviceName}", Version = apiVersion });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                if (env.IsDevelopment() || env.EnvironmentName.ToLower() == "local")
                {
                    c.AddSecurityDefinition("i-api-key", new OpenApiSecurityScheme
                    {
                        Description = "For internal network calls.\r\n\r\nExample: \"i-api-key 12345abcdef\"",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "i-api-key"
                    });
                }

                var securityRequirements = new OpenApiSecurityRequirement()
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
                };

                if (env.IsDevelopment() || env.EnvironmentName.ToLower() == "local")
                {
                    securityRequirements.Add(new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "i-api-key"
                        },
                        Scheme = "api-key",
                        Name = "i-api-key",
                        In = ParameterLocation.Header,

                    }, new List<string>());
                }

                c.AddSecurityRequirement(securityRequirements);
            });

            return services;
        }
    }
}
