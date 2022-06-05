using Microsoft.AspNetCore.Mvc;
using NJsonSchema;
using System.Net;
using System.Reflection;

namespace Simplic.OxS.Hook
{
    [ApiController]
    [Route("[controller]")]
    public class HookDefinitionController : ControllerBase
    {
        private readonly HookDefinitionService service;

        public HookDefinitionController(HookDefinitionService service)
        {
            this.service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(Model.HookDefinitionResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Get()
        {
            var result = new Model.HookDefinitionResponse();

            foreach (var type in service.Definitions)
            {
                var schema = JsonSchema.FromType(type.Key);

                result.Definitions.Add(new Model.HookDefinitionModel
                {
                    Name = type.Value.Name,
                    DataType = type.Value.DataType,
                    Description = type.Value.Description,
                    Operation = type.Value.Operation,
                    Payload = schema.ToJson()
                });
            }

            return Ok(result);
        }
    }
}