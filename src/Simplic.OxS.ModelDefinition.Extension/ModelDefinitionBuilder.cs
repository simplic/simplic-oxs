using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Simplic.OxS.ModelDefinition.Service;
using System.Text.Json;

namespace Simplic.OxS.ModelDefinition.Extension
{
    public static class ModelDefinitionBuilder
    {
        /// <summary>
        /// Writes the model definition document to <c>ModelDefinition/ModelDefinition.json</c>.
        /// Nothing reads that file: a host built on <c>Bootstrap</c> builds the document in memory
        /// from the controllers its startup returns from <c>ConfigureModelDefinitions()</c>, and
        /// serves it from there. A service calls nothing in its place.
        /// </summary>
        [Obsolete("Delete this call: nothing reads the file it writes. The host builds the model definition document in memory, from the controllers the startup returns from ConfigureModelDefinitions(), and serves it.")]
        public static void AddControllerDefinitions(
            this IApplicationBuilder app,
            IWebHostEnvironment env,
            string basePath,
            IList<Type> controllers)
        {
            var directoryPath = Path.Combine(env.ContentRootPath, "ModelDefinition");
            var filePath = Path.Combine(directoryPath, "ModelDefinition.json");

            Console.Out.WriteLineAsync($"Builder filepath: {filePath}").GetAwaiter().GetResult();
            try
            {
                var definitions = new List<ModelDefinition>();

                foreach (var controller in controllers)
                {
                    var definition = ModelDefinitionService.GenerateDefinitionForController(controller);
                    Console.Out.WriteLineAsync($"Builder definition: {definition}").GetAwaiter().GetResult();
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
        }
    }
}
