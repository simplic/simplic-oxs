using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.XPath;

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

                // Declare the standard problem+json error responses on every operation.
                c.OperationFilter<ProblemDetailsOperationFilter>();

                // The query and schema routes of this package: what their attributes cannot say.
                c.OperationFilter<DescribedRouteOperationFilter>();

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
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

                // Add global security requirements using the newer API
                c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
                    [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
                });

                // Internal API Key security requirement for development/local environments
                if (env.IsDevelopment() || env.EnvironmentName.ToLower() == "local")
                {
                    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecuritySchemeReference(Constants.HttpAuthorizationSchemeInternalKey, null, null),
                            new List<string>()
                        }
                    });
                }
            });

            // The comments of the query engine's assemblies describe the parameters of the OxQL
            // routes and the members of their bodies. The package's build targets put the files
            // beside the service's assemblies; a host without them describes the routes without
            // those sentences.
            services.AddOptions<SwaggerGenOptions>().Configure<ILoggerFactory>((options, loggers) =>
                IncludeQueryEngineComments(options, AppContext.BaseDirectory, loggers.CreateLogger("Simplic.OxS.Server.OpenApi")));

            // The package's own comments describe the models of the schema document, and nothing
            // else of the package: the same targets put that file there.
            services.AddOptions<SwaggerGenOptions>().Configure<ILoggerFactory>((options, loggers) =>
                PackageComments.Include(options, AppContext.BaseDirectory, loggers.CreateLogger("Simplic.OxS.Server.OpenApi")));

            return services;
        }

        /// <summary>
        /// Includes every <c>OxQL.*.xml</c> of <paramref name="directory"/> in the document's
        /// comments, in the order of their names. A file that cannot be read (it is no XML, or
        /// the host may not open it) is left out with a warning: the routes are then described
        /// without its sentences, and the document is generated all the same.
        /// </summary>
        internal static void IncludeQueryEngineComments(SwaggerGenOptions options, string directory, ILogger logger)
        {
            List<string> files;

            try
            {
                files = [.. Directory.EnumerateFiles(directory, "OxQL.*.xml").Order(StringComparer.Ordinal)];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "The OpenAPI document carries no comments of the query engine: {Directory} cannot be listed", directory);

                return;
            }

            foreach (var file in files)
            {
                XPathDocument comments;

                try
                {
                    comments = new XPathDocument(file);
                }
                catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(exception, "The OpenAPI document leaves out the comments of {File}: the file cannot be read", System.IO.Path.GetFileName(file));

                    continue;
                }

                options.IncludeXmlComments(() => comments);
            }
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
