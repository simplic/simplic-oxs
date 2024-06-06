using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinition.Extension
{
    public class ModelDefinitionMiddleware 
    {
        private readonly StaticFileMiddleware staticFileMiddleware;

        public ModelDefinitionMiddleware(RequestDelegate next,
            IWebHostEnvironment env,
            ILoggerFactory loggerFactory,
            string basePath)
        {
            staticFileMiddleware = ModelDefinitionBuilder.BuildStaticFileMiddleware(next, env, basePath, loggerFactory);
        }

        public async Task Invoke(HttpContext context, RequestDelegate next)
        {
            await staticFileMiddleware.Invoke(context);
        }
    }
}
