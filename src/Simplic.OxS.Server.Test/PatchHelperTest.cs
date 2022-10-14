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
        public void Patch_SingleFieldJson_PatchesSingleField()
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

            var patchedTestPerson = PatchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.LastName.Should().Be("Doe");
            patchedTestPerson.FirstName.Should().Be("John");
        }

        [Fact]
        public void Patch_AllFieldJson_PatchesMultipleFields()
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

            var patchedTestPerson = PatchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.FirstName.Should().Be("John");
            patchedTestPerson.LastName.Should().Be("Doe");
        }

        [Fact]
        public void Patch_SingleFieldJsonMultiFieldMap_JustPatchesJsonFields()
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

            var patchedTestPerson = PatchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.LastName.Should().Be("Doe");
            patchedTestPerson.FirstName.Should().Be("John");
        }

        [Fact]
        public void Patch_ListUpdateContent_UpdatesTheItem()
        {
            var guid = Guid.NewGuid();

            var originalTestPerson = new TestPerson();
            originalTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                Id = guid,
                PhoneNumber = "1234"
            });

            var mappedTestPerson = new TestPerson();
            mappedTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                Id = guid,
                PhoneNumber = "5678"
            });

            var json = @"{""PhoneNumbers"" : [{""Id"": """ + guid.ToString() + @""", ""PhoneNumber"" : ""5678"" }]}";

            var patchedTestPerson = PatchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.First().PhoneNumber.Should().Be("5678");
        }

        [Fact]
        public void Patch_ListUpdateContent_UpdatesTheRightItem()
        {
            var guid = Guid.NewGuid();

            var originalTestPerson = new TestPerson();
            originalTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                Id = guid,
                PhoneNumber = "1234"
            });
            originalTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                Id = Guid.NewGuid(),
                PhoneNumber = "1234"
            });

            var mappedTestPerson = new TestPerson();
            mappedTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                Id = guid,
                PhoneNumber = "5678"
            });

            var json = @"{""PhoneNumbers"" : [{""Id"": """ + guid.ToString() + @""", ""PhoneNumber"" : ""5678"" }]}";

            var patchedTestPerson = PatchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.First(x => x.Id == guid).PhoneNumber.Should().Be("5678");
            patchedTestPerson.PhoneNumbers.First(x => x.Id != guid).PhoneNumber.Should().Be("1234");
            patchedTestPerson.PhoneNumbers.Count.Should().Be(2);
        }

        [Fact]
        public void Patch_ListRemoveContent_UpdatesTheItem()
        {
            var guid = Guid.NewGuid();

            var originalTestPerson = new TestPerson();
            originalTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                Id = guid,
                PhoneNumber = "1234"
            });

            var mappedTestPerson = new TestPerson();
            mappedTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                Id = guid,
            });

            var json = @"{""PhoneNumbers"" : [{ ""Id"" : """ + guid.ToString() + @""", ""_remove"" : true }]}";

            var patchedTestPerson = PatchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.Count().Should().Be(0);
        }

        [Fact]
        public void Patch_ListAddContent_UpdatesTheItem()
        {
            var originalTestPerson = new TestPerson();

            var mappedTestPerson = new TestPerson();
            mappedTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                PhoneNumber = "5678"
            });

            var json = @"{""PhoneNumbers"" : [{ ""PhoneNumber"" : ""5678"" }]}";

            var patchedTestPerson = PatchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.First().PhoneNumber.Should().Be("5678");
        }

        [Fact]
        public void Patch_FalseReturningValidationRequest_ThrowsExcpetion()
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

            this.Invoking(x => PatchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return false;
            })).Should().Throw<BadRequestException>();
        }

        [Fact]
        public void Patch_ValidationForProperty_ThrowsExcpetion()
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

            this.Invoking(x => PatchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                if(validation.Property == "LastName")
                    return false;

                return true;
            })).Should().Throw<BadRequestException>();
        }

        [Fact]
        public void Patch_ValidationForValue_ThrowsExcpetion()
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

            this.Invoking(x => PatchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                if (validation.Value == "Doe")
                    return false;

                return true;
            })).Should().Throw<BadRequestException>();
        }

        [Fact]
        public void Patch_ValidationForPath_ThrowsExcpetion()
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

            this.Invoking(x => PatchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                if (validation.Path == "LastName")
                    return false;

                return true;
            })).Should().Throw<BadRequestException>();
        }

        [Fact]
        public void Patch_JsonWithPropertyNotInObjects_ThrowsExcpetion()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var mappedTestPerson = new TestPerson
            {
            };

            var json = @"{""Street"" : ""Does Not Exist Street""}";

            this.Invoking(x => PatchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                if (validation.Path == "LastName")
                    return false;

                return true;
            })).Should().Throw<BadRequestException>();
        }
    }
}
