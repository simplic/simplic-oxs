using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.ModelDefinition;
using Simplic.OxS.ServiceDefinition;
using System.Text.Json;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Controller for returning the model definition JSON, optionally extended with
    /// organization-specific addon fields when a valid auth token is present.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("/[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ModelDefinitionController : ControllerBase
    {
        private readonly IWebHostEnvironment env;
        private readonly IAddonFieldRepository addonFieldRepository;
        private readonly IRequestContext requestContext;

        public ModelDefinitionController(
            IWebHostEnvironment env,
            IAddonFieldRepository addonFieldRepository,
            IRequestContext requestContext)
        {
            this.env = env;
            this.addonFieldRepository = addonFieldRepository;
            this.requestContext = requestContext;
        }

        /// <summary>
        /// Returns the model definition. When a valid auth token is provided the response
        /// is extended with the organization's addon field configurations.
        /// </summary>
        public async Task<ActionResult> Get()
        {
            var filePath = System.IO.Path.Combine(env.ContentRootPath, "ModelDefinition", "ModelDefinition.json");

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            byte[] content;

            try
            {
                content = System.IO.File.ReadAllBytes(filePath);
            }
            catch (Exception)
            {
                return BadRequest();
            }

            if (content == null)
                return NotFound();

            // When the request carries a valid auth token (OrganizationId is populated by
            // RequestContextActionFilter) extend the definition with addon fields.
            if (requestContext.OrganizationId.HasValue)
            {
                try
                {
                    var modelDef = JsonSerializer.Deserialize<ModelDefinition.ModelDefinition>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (modelDef != null)
                    {
                        var addonFields = (await addonFieldRepository.GetAllAsync())
                            .Where(f => f.ObjectName == modelDef.Model)
                            .ToList();

                        if (addonFields.Count > 0)
                        {
                            const string addonRefName = "$Addon";

                            // Add a single "addon" property entry pointing to the $Addon reference,
                            // mirroring how a complex-type property is represented (e.g. public Addon Addon).
                            if (!modelDef.Properties.Any(p => p.Name == "addon"))
                            {
                                modelDef.Properties.Add(new PropertyDefinition
                                {
                                    Name = "addon",
                                    Type = addonRefName,
                                    Nullable = true
                                });
                            }

                            // Build or replace the $Addon reference definition, one property per addon field,
                            // exactly as BuildProperties() would for a real class.
                            var addonRef = modelDef.References.FirstOrDefault(r => r.Model == addonRefName);
                            if (addonRef == null)
                            {
                                addonRef = new ReferenceDefinition
                                {
                                    Model = addonRefName,
                                    Title = "Addon",
                                    Properties = new List<PropertyDefinition>()
                                };
                                modelDef.References.Add(addonRef);
                            }

                            addonRef.Properties = addonFields.Select(f => new PropertyDefinition
                            {
                                Name = f.PropertyName,
                                Type = f.PropertyType,
                                Description = f.Description,
                                Nullable = true
                            }).ToList();
                        }

                        var extended = JsonSerializer.SerializeToUtf8Bytes(modelDef,
                            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                        return base.File(extended, "application/json");
                    }
                }
                catch (JsonException)
                {
                    // If deserialization fails, fall through and return the raw file.
                }
            }

            return base.File(content, "application/json");
        }
    }
}
