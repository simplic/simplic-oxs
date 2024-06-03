using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.ModelDefinition.Extenstion.Abstractions;
using Simplic.OxS.Server.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinitionExtension.Test.TestEnv
{
    internal class TestController : OxSController
    {
        public TestController()
        {
        }

        [ModelDefinitionGetOperation("/test/get", typeof(TestResponse))]
        public void Get() { }

        [ModelDefinitionPostOperation("/test/post", typeof(TestRequest), typeof(TestResponse))]
        public void Post() { }

        [ModelDefinitionPatchOperation("/test/patch", typeof(TestRequest), typeof(TestResponse))]
        public void Patch() { }

        [ModelDefinitionDeleteOperation("/test/delete")]
        public void Delete() { }
    }

}
