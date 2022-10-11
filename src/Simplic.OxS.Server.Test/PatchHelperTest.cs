using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Simplic.OxS.Server;

namespace Simplic.OxS.Server.Test
{
    public class PatchHelperTest
    {
        [Fact]
        public async Task Patch_SingleFieldJson_PatchesSingleField()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var mappedTestPerson = new TestPerson
            {
                LastName = "Doe"
            };

            var json = @"{""LastName"" : ""Doe""}";

            var patchedTestPerson = PatchHelper.CreatePatch<TestPerson, Guid>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.LastName.Should().Be("Doe");
        }

        [Fact]
        public async Task Patch_AllFieldJson_PatchesMultipleFields()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "Max",
                LastName = "Mustermann"
            };

            var mappedTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Doe"
            };

            var json = @"{
                            ""FirstName"": ""John"",
                            ""LastName"": ""Doe""
                        }";

            var patchedTestPerson = PatchHelper.CreatePatch<TestPerson, Guid>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.FirstName.Should().Be("John");
            patchedTestPerson.LastName.Should().Be("Doe");
        }

        [Fact]
        public async Task Patch_SingleFieldJsonMultiFieldMap_JustPatchesJsonFields()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var mappedTestPerson = new TestPerson
            {
                FirstName = "Max",
                LastName = "Doe"
            };

            var json = @"{""LastName"" : ""Doe""}";

            var patchedTestPerson = PatchHelper.CreatePatch<TestPerson, Guid>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.LastName.Should().Be("Doe");
            patchedTestPerson.FirstName.Should().Be("John");
        }
    }
}
