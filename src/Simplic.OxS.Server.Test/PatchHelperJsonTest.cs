using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Test
{
    public class PatchHelperJsonTest
    {
        [Fact]
        public void EnumeratePaths_SinglePropertyJson_ReturnsSinglePath()
        {
            var json = @"{""LastName"" : ""Doe""}";
            using var document = JsonDocument.Parse(json);
            var res = PatchHelper.EnumeratePaths(document.RootElement).ToList();
            res.Should().ContainSingle();
        }

        [Fact]
        public void EnumeratePaths_MultiPropertyJson_ReturningAllPaths()
        {
            var json = @"{""LastName"" : ""Doe"", ""FirstName"": ""John""}";
            using var document = JsonDocument.Parse(json);
            var res = PatchHelper.EnumeratePaths(document.RootElement).ToList();
            res.Should().Contain("LastName", "FirstName");
        }

        [Fact]
        public void EnumeratePaths_NestedObjectJson_ReturnsNestedPaths()
        {
            var json = @"{ 
                            ""LastName"" : ""Doe"", 
                            ""FirstName"": ""John"",
                            ""Address"": {
                                ""Street"": ""Street str.""
                            }
                          }";

            using var document = JsonDocument.Parse(json);
            var res = PatchHelper.EnumeratePaths(document.RootElement).ToList();
            res.Should().Contain("LastName", "FirstName", "Address.Street");
        }
    }
}
