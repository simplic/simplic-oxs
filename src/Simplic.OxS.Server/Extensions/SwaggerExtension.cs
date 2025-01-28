using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Runtime.Serialization;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// Swagger extension methods
    /// </summary>
    internal static class SwaggerExtension
    {
        /// <summary>
        /// Add swagger to the actual service/endpoint
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="env">Actual env</param>
        /// <param name="apiVersion">Current application version</param>
        /// <param name="serviceName">Current service name</param>
        /// <param name="info">Open API information</param>
        /// <returns>Service collection</returns>
        internal static IServiceCollection AddSwagger(this IServiceCollection services, IWebHostEnvironment env, string apiVersion, string serviceName, OpenApiInfo info)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc(apiVersion, new OpenApiInfo { Title = $"Simplic.OxS.{serviceName}", Version = apiVersion });
                c.SwaggerDoc($"{apiVersion}-SignalR", new OpenApiInfo { Title = $"Simplic.OxS.{serviceName} with SignalR", Version = apiVersion });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                // Add API Key Authentication
                c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Name = Constants.HttpHeaderApiKey,
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Description = "API Key authentication. Example: 'x-api-key: {your-api-key}'"
                });

                var knownTypes = GetKnownTypes();
                c.UseOneOfForPolymorphism();
                c.SelectSubTypesUsing(baseType =>
                {
                    if (knownTypes.ContainsKey(baseType))
                    {
                        return knownTypes[baseType];
                    }
                    return Enumerable.Empty<Type>();
                });

                if (env.IsDevelopment() || env.EnvironmentName.ToLower() == "local")
                {
                    c.AddSecurityDefinition(Constants.HttpAuthorizationSchemeInternalKey, new OpenApiSecurityScheme
                    {
                        Description = "For internal network calls.\r\n\r\nExample: \"i-api-key 12345abcdef\"",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = Constants.HttpAuthorizationSchemeInternalKey
                    });
                }

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetEntryAssembly().GetName().Name}.xml";
                var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);

                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                    Console.WriteLine($"Use xml documentation file `{xmlPath}`.");
                }
                else
                {
                    Console.WriteLine($"No xml documentation file found under `{xmlPath}`. https://docs.microsoft.com/en-us/samples/aspnet/aspnetcore.docs/getstarted-swashbuckle-aspnetcore/?tabs=visual-studio");
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
                    },
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "ApiKey"
                            },
                            In = ParameterLocation.Header,
                        },
                        Array.Empty<string>()
                    }
                };

                if (env.IsDevelopment() || env.EnvironmentName.ToLower() == "local")
                {
                    securityRequirements.Add(new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = Constants.HttpAuthorizationSchemeInternalKey
                        },
                        Scheme = Constants.HttpAuthorizationSchemeInternalKey,
                        Name = Constants.HttpAuthorizationSchemeInternalKey,
                        In = ParameterLocation.Header,

                    }, new List<string>());
                }

                c.AddSecurityRequirement(securityRequirements);
                // c.DocumentFilter<HideInternalAPIFilter>();

                c.AddSignalRSwaggerGen(so =>
                {
                    so.AutoDiscover = SignalRSwaggerGen.Enums.AutoDiscover.MethodsAndParams;
                    so.DisplayInDocuments($"{apiVersion}-SignalR");
                    if (File.Exists(xmlPath))
                    {
                        so.UseXmlComments(xmlPath);
                    }
                });
            });

            return services;
        }

        public class HideInternalAPIFilter : IDocumentFilter
        {
            public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
            {
                // TODO: Hide internal on public server
            }
        }

        /// <summary>
        /// Creates a dictionary of all base types that has the attribute <see cref="KnownTypeAttribute"/>
        /// </summary>
        /// <returns>Dictionary of type mappings for api abstraction/polymorphism</returns>
        private static IDictionary<Type, IList<Type>> GetKnownTypes()
        {
            var dictionary = new Dictionary<Type, IList<Type>>();

            foreach (var type in Assembly.GetEntryAssembly().GetTypes())
            {
                foreach (var knownType in type.GetCustomAttributes().OfType<KnownTypeAttribute>())
                {
                    if (dictionary.ContainsKey(type))
                    {
                        dictionary[type].Add(knownType.Type);
                    }
                    else
                    {
                        dictionary.Add(type, new List<Type> { knownType.Type });
                    }

                }
            }

            return dictionary;
        }
    }
}
