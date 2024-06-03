﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
            app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(directoryPath),
                    RequestPath = $"{basePath}/ModelDefinition"
                });
            
        }
    }
}