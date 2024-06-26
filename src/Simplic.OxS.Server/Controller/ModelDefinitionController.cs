﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Controller for supporting k8s health checks
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("/[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ModelDefinitionController : ControllerBase
    {
        private readonly IWebHostEnvironment env;

        public ModelDefinitionController(IWebHostEnvironment env)
        {
            this.env = env;
        }

        /// <summary>
        /// Return status code 200
        /// </summary>
        /// <returns>Status code 200</returns>
        public async Task<ActionResult> Get()
        {
            var filePath = System.IO.Path.Combine(env.ContentRootPath, "ModelDefinition", "ModelDefinition.json");

            await Console.Out.WriteLineAsync($"{filePath}");

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            byte[] content;

            try
            {
                content = System.IO.File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.ToString());
                return BadRequest();
            }

            if (content != null)
                return base.File(content, "application/json");

            return NotFound();
        }
    }
}
