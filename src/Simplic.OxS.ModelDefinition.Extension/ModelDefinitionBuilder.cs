using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Simplic.OxS.ModelDefinition.Service;
using System.Text.Json;

namespace Simplic.OxS.ModelDefinition.Extension
{
    public static class ModelDefinitionBuilder
    {
        public static void AddControllerDefinitions(this IApplicationBuilder app, IWebHostEnvironment env, string basePath, IList<Type> controllers)
        {


            var directoryPath = Path.Combine(env.ContentRootPath, "ModelDefinition");
            var filePath = Path.Combine(directoryPath, "ModelDefinition.json");

            try
            {
                var definitions = new List<ModelDefinition>();

                foreach (var controller in controllers)
                {
                    var definition = ModelDefinitionService.GenerateDefinitionForController(controller);
                    definitions.Add(definition);
                }

                var json = JsonSerializer.Serialize(definitions,
                    new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                        WriteIndented = true,
                    });


                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {

                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                File.WriteAllText(filePath, ex.ToString());
            }

            // Add a custom middleware to handle the GET request
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.Equals($"{basePath}/ModelDefinition"))
                {
                    if (File.Exists(filePath))
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.SendFileAsync(filePath);
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("ModelDefinition.json not found");
                    }
                }
                else
                {
                    await next();
                }
            });            
        }
    }
}
