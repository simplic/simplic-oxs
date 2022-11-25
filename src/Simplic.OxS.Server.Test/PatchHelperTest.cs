using FluentAssertions;
using System.Text;

namespace Simplic.OxS.Server.Test
{
    /// <summary>
    /// Class to test the patch helper in.
    /// </summary>
    public class PatchHelperTest
    {
        /// <summary>
        /// Tests whether the patch method will patch a single and the right property 
        /// when called with the right parameters.
        /// </summary>
        [Fact]
        public async Task Patch_SingleFieldJson_PatchesSingleField()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                LastName = "Doe"
            };

            var json = @"{""LastName"" : ""Doe""}";

            var patchHelper = new PatchHelper();

            var patchedTestPerson = await patchHelper.Patch(originalTestPerson, patchRequest, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.LastName.Should().Be("Doe");
            patchedTestPerson.FirstName.Should().Be("John");
        }

        /// <summary>
        /// Tests whether the patch method will patch a single and the right property 
        /// when called with the right parameters (ignore property case).
        /// </summary>
        [Fact]
        public async Task Patch_SingleFieldJson_PatchesSingleField_IgnoreCase()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                LastName = "Doe"
            };

            // Use lower-case property
            var json = @"{""lastName"" : ""Doe""}";

            var patchHelper = new PatchHelper();

            var patchedTestPerson = await patchHelper.Patch(originalTestPerson, patchRequest, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.LastName.Should().Be("Doe");
            patchedTestPerson.FirstName.Should().Be("John");
        }

        /// <summary>
        /// Tests whether the patch method will patch multiple properties of a json to the right values 
        /// in the original object.
        /// </summary>
        [Fact]
        public async Task Patch_AllFieldJson_PatchesMultipleFields()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "Max",
                LastName = "Mustermann"
            };

            var patchReqeust = new TestPersonRequest
            {
                FirstName = "John",
                LastName = "Doe"
            };

            var json = @"{
                            ""FirstName"": ""John"",
                            ""LastName"": ""Doe""
                        }";

            var patchHelper = new PatchHelper();

            var patchedTestPerson =await patchHelper.Patch<TestPerson>(originalTestPerson, patchReqeust, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.FirstName.Should().Be("John");
            patchedTestPerson.LastName.Should().Be("Doe");
        }

        /// <summary>
        /// Tests whether the patch method will patch only the properties that are contained in the json even 
        /// when more properties are set in the mapped object.
        /// </summary>
        [Fact]
        public async Task Patch_SingleFieldJsonMultiFieldMap_JustPatchesJsonFields()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                FirstName = "Max",
                LastName = "Doe"
            };

            var json = @"{""LastName"" : ""Doe""}";

            var patchHelper = new PatchHelper();

            var patchedTestPerson =await patchHelper.Patch<TestPerson>(originalTestPerson, patchRequest, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.LastName.Should().Be("Doe");
            patchedTestPerson.FirstName.Should().Be("John");
        }

        /// <summary>
        /// Tests whether the patch method will update properties of a list when the id of the items are equal.
        /// </summary>
        [Fact]
        public async Task Patch_ListUpdateContent_UpdatesTheItem()
        {
            var guid = Guid.NewGuid();

            var originalTestPerson = new TestPerson();
            originalTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                Id = guid,
                PhoneNumber = "1234"
            });

            var patchRequest = new TestPersonRequest();
            patchRequest.PhoneNumbers.Add(new TestPhoneNumberRequest
            {
                Id = guid,
                PhoneNumber = "5678"
            });

            var json = @"{""PhoneNumbers"" : [{""Id"": """ + guid.ToString() + @""", ""PhoneNumber"" : ""5678"" }]}";

            var patchHelper = new PatchHelper();

            var patchedTestPerson =await patchHelper.Patch(originalTestPerson, patchRequest, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.First().PhoneNumber.Should().Be("5678");
        }

        /// <summary>
        /// Tests whether the patch method will update the properties of the right item in a list instead of just 
        /// taking the first item.
        /// </summary>
        [Fact]
        public async Task Patch_ListUpdateContent_UpdatesTheRightItem()
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

            var patchReqeust = new TestPersonRequest();
            patchReqeust.PhoneNumbers.Add(new TestPhoneNumberRequest
            {
                Id = guid,
                PhoneNumber = "5678"
            });

            var json = @"{""PhoneNumbers"" : [{""Id"": """ + guid.ToString() + @""", ""PhoneNumber"" : ""5678"" }]}";

            var patchHelper = new PatchHelper();

            var patchedTestPerson = await patchHelper.Patch<TestPerson>(originalTestPerson, patchReqeust, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.First(x => x.Id == guid).PhoneNumber.Should().Be("5678");
            patchedTestPerson.PhoneNumbers.First(x => x.Id != guid).PhoneNumber.Should().Be("1234");
            patchedTestPerson.PhoneNumbers.Count.Should().Be(2);
        }

        /// <summary>
        /// Tests whether the patch method will hard delete items when they are send with a '_remove = true' flag.
        /// </summary>
        [Fact]
        public async Task Patch_ListRemoveContent_RemovesTheItem()
        {
            var guid = Guid.NewGuid();

            var originalTestPerson = new TestPerson();
            originalTestPerson.PhoneNumbers.Add(new TestPhoneNumber
            {
                Id = guid,
                PhoneNumber = "1234"
            });

            var patchRequest = new TestPersonRequest();
            patchRequest.PhoneNumbers.Add(new TestPhoneNumberRequest
            {
                Id = guid,
            });

            var json = @"{""PhoneNumbers"" : [{ ""Id"" : """ + guid.ToString() + @""", ""_remove"" : true }]}";

            var patchHelper = new PatchHelper();

            var patchedTestPerson = await patchHelper.Patch<TestPerson>(originalTestPerson, patchRequest, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.Count().Should().Be(0);
        }

        /// <summary>
        /// Tests whether the patch method will add a new item to the original object.
        /// </summary>
        [Fact]
        public async Task Patch_ListAddContent_AddsTheItem()
        {
            var originalTestPerson = new TestPerson();

            var patchRequest = new TestPersonRequest();
            patchRequest.PhoneNumbers.Add(new TestPhoneNumberRequest
            {
                PhoneNumber = "5678"
            });

            var json = @"{""PhoneNumbers"" : [{ ""PhoneNumber"" : ""5678"" }]}";

            var patchHelper = new PatchHelper();

            var patchedTestPerson = await patchHelper.Patch<TestPerson>(originalTestPerson, patchRequest, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.First().PhoneNumber.Should().Be("5678");
        }

        /// <summary>
        /// Tests whether the patch method will throw an exception when the validation request returns false.
        /// </summary>
        [Fact]
        public async Task Patch_FalseReturningValidationRequest_ThrowsExcpetion()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                LastName = "Doe"
            };

            var json = @"{""LastName"" : ""Doe""}";

            var patchHelper = new PatchHelper();

            await this.Invoking( x => patchHelper.Patch(originalTestPerson, patchRequest, json, (validation) =>
            {
                return false;
            })).Should().ThrowAsync<BadRequestException>();
        }

        /// <summary>
        /// Tests whether the patch method will set the property name in validation requests. 
        /// </summary>
        [Fact]
        public async Task Patch_ValidationForProperty_ThrowsExcpetion()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                LastName = "Doe"
            };

            var json = @"{""LastName"" : ""Doe""}";

            var patchHelper = new PatchHelper();

            await this.Invoking(x => patchHelper.Patch(originalTestPerson, patchRequest, json, (validation) =>
            {
                if (validation.Property == "LastName")
                    return false;

                return true;
            })).Should().ThrowAsync<BadRequestException>();
        }

        /// <summary>
        /// Tests whether the patch method will set the value in validation requests. 
        /// </summary>
        [Fact]
        public async Task Patch_ValidationForValue_ThrowsExcpetion()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                LastName = "Doe"
            };

            var json = @"{""LastName"" : ""Doe""}";

            var patchHelper = new PatchHelper();

            await this.Invoking(x => patchHelper.Patch(originalTestPerson, patchRequest, json, (validation) =>
            {
                if (validation.Value == "Doe")
                    return false;

                return true;
            })).Should().ThrowAsync<BadRequestException>();
        }

        /// <summary>
        /// Tests whether the patch method will set the path in validation requests. 
        /// </summary>
        [Fact]
        public async Task Patch_ValidationForPath_ThrowsExcpetion()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                LastName = "Doe"
            };

            var json = @"{""LastName"" : ""Doe""}";

            var patchHelper = new PatchHelper();

            await this.Invoking(x => patchHelper.Patch(originalTestPerson, patchRequest, json, (validation) =>
            {
                if (validation.Path == "LastName")
                    return false;

                return true;
            })).Should().ThrowAsync<BadRequestException>();
        }

        /// <summary>
        /// Tests whether the patch method will throw a bad request exception when a property is contained 
        /// in the json but is not part of the object.
        /// </summary>
        [Fact]
        public async Task Patch_JsonWithPropertyNotInObjects_ThrowsExcpetion()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "John",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
            };

            var json = @"{""Street"" : ""Does Not Exist Street""}";

            var patchHelper = new PatchHelper();

            await this.Invoking(x => patchHelper.Patch(originalTestPerson, patchRequest, json, (validation) =>
            {
                if (validation.Path == "LastName")
                    return false;

                return true;
            })).Should().ThrowAsync<BadRequestException>();
        }

        /// <summary>
        /// Test whether validation returns always truem if no validation (null) is passed
        /// </summary>
        [Fact]
        public async Task Patch_AllFieldJson_ValidateAll()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "Max",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                FirstName = "John",
                LastName = "Doe"
            };

            var json = @"{
                            ""FirstName"": ""John"",
                            ""LastName"": ""Doe""
                        }";

            var patchHelper = new PatchHelper();

            var patchedTestPerson = await patchHelper.Patch<TestPerson>(originalTestPerson, patchRequest, json, null);

            patchedTestPerson.FirstName.Should().Be("John");
            patchedTestPerson.LastName.Should().Be("Doe");
        }

        /// <summary>
        /// Check whether an inner exception is passed for invalid json
        /// </summary>
        [Fact]
        public async Task Patch_CatchInvalidJsonException()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "Max",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                FirstName = "John",
                LastName = "Doe"
            };

            var json = @"{
                            ""FirstName"": ""John"",
                            ""LastName"": ""Doe""
                        this is invalid ....";

            var patchHelper = new PatchHelper();

            (await this.Invoking(x => patchHelper.Patch(originalTestPerson, patchRequest, json, null))
                .Should().ThrowAsync<ArgumentException>())
                .WithInnerException<System.Text.Json.JsonException>();
        }


        /// <summary>
        /// Test whether patch will apply changes properly
        /// </summary>
        [Fact]
        public async Task Patch_AllFieldJson_Configuration()
        {
            var originalTestPerson = new TestPerson
            {
                FirstName = "Max",
                LastName = "Mustermann"
            };

            var patchRequest = new TestPersonRequest
            {
                FirstName = "John",
                LastName = "Doe"
            };

            var json = @"{
                            ""FirstName"": ""John"",
                            ""LastName"": ""Doe""
                        }";

            var patchHelper = new PatchHelper(cfg =>
            {
                cfg.ForPath("FirstName").ChangeAction<TestPerson, TestPersonRequest>((original, patch) => { original.FirstName = "Peter"; return Task.CompletedTask; });
                return cfg;
            });

            var patchedTestPerson = await patchHelper.Patch<TestPerson>(originalTestPerson, patchRequest, json, null);

            patchedTestPerson.FirstName.Should().Be("Peter");
            patchedTestPerson.LastName.Should().Be("Doe");
        }

        /// <summary>
        /// Tests whether the patch method will apply changes correctly when the properties of the patch object are nullable
        /// but the proeprties in the original object are not.
        /// </summary>
        [Fact]
        public async Task Patch_AllTypes_AllDataIsWritten()
        {
            var originalTestPerson = new TestPerson
            {
                TestBool = false,
                TestDateTime = default,
                TestDouble = default,
                TestGuid = Guid.Empty,
                TestInt = default
            };

            var mappedTestPerson = new TestPersonRequest
            {
                TestBool = true,
                TestDateTime = DateTime.Now,
                TestDouble = 123.11,
                TestGuid = Guid.NewGuid(),
                TestInt = 132
            };

            var bobTheBuilder = new StringBuilder();
            bobTheBuilder.AppendLine("{");
            bobTheBuilder.AppendLine($@"""TestBool"" : {(mappedTestPerson.TestBool.Value ? 1 : 0 )},");
            bobTheBuilder.AppendLine($@"""TestDateTime"" : ""{mappedTestPerson.TestDateTime}"",");
            bobTheBuilder.AppendLine($@"""TestDouble"" : ""{mappedTestPerson.TestDouble}"",");
            bobTheBuilder.AppendLine($@"""TestGuid"" : ""{mappedTestPerson.TestGuid}"",");
            bobTheBuilder.AppendLine($@"""TestInt"" : {mappedTestPerson.TestInt}");
            bobTheBuilder.AppendLine("}");

            var json = bobTheBuilder.ToString();

            var patchHelper = new PatchHelper();

            var patchedTestPerson = await patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, null);

            patchedTestPerson.TestBool.Should().Be(mappedTestPerson.TestBool.Value);
            patchedTestPerson.TestDateTime.Should().Be(mappedTestPerson.TestDateTime);
            patchedTestPerson.TestDouble.Should().Be(mappedTestPerson.TestDouble);
            patchedTestPerson.TestGuid.Should().Be(mappedTestPerson.TestGuid.Value);
            patchedTestPerson.TestInt.Should().Be(mappedTestPerson.TestInt);
        }

        /// <summary>
        /// Tests whether the patch method will apply changes correctly when the json has just lower case properties.
        /// </summary>
        [Fact]
        public async Task Patch_LowerCaseJson_AllDataIsWritten()
        {
            var originalTestPerson = new TestPerson
            {
                TestBool = false,
                TestDateTime = default,
                TestDouble = default,
                TestGuid = Guid.Empty,
                TestInt = default,
                LastName = "Max"
            };

            var mappedTestPerson = new TestPersonRequest
            {
                TestBool = true,
                TestDateTime = DateTime.Now,
                TestDouble = 123.11,
                TestGuid = Guid.NewGuid(),
                TestInt = 132,
                LastName = "Mustemann"
            };

            var bobTheBuilder = new StringBuilder();
            bobTheBuilder.AppendLine("{");
            bobTheBuilder.AppendLine($@"""testBool"" : {(mappedTestPerson.TestBool.Value ? 1 : 0)},");
            bobTheBuilder.AppendLine($@"""testDateTime"" : ""{mappedTestPerson.TestDateTime}"",");
            bobTheBuilder.AppendLine($@"""testDouble"" : ""{mappedTestPerson.TestDouble}"",");
            bobTheBuilder.AppendLine($@"""testGuid"" : ""{mappedTestPerson.TestGuid}"",");
            bobTheBuilder.AppendLine($@"""testInt"" : {mappedTestPerson.TestInt},");
            bobTheBuilder.AppendLine($@"""lastName"" : ""{mappedTestPerson.LastName}""");
            bobTheBuilder.AppendLine("}");

            var json = bobTheBuilder.ToString();

            var patchHelper = new PatchHelper(cfg =>
            {
                cfg.ForPath("LastName").ChangeAction<TestPerson, TestPersonRequest>((original, patch) =>
                {
                    original.LastName = "Doe";
                    return Task.CompletedTask;
                });
                return cfg;
            });

            var patchedTestPerson = await patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, null);

            patchedTestPerson.TestBool.Should().Be(mappedTestPerson.TestBool.Value);
            patchedTestPerson.TestDateTime.Should().Be(mappedTestPerson.TestDateTime);
            patchedTestPerson.TestDouble.Should().Be(mappedTestPerson.TestDouble);
            patchedTestPerson.TestGuid.Should().Be(mappedTestPerson.TestGuid.Value);
            patchedTestPerson.TestInt.Should().Be(mappedTestPerson.TestInt);
            patchedTestPerson.LastName.Should().Be("Doe");
        }

        /// <summary>
        /// Tests whether the patch method will add a new item to the original object.
        /// </summary>
        [Fact]
        public async Task Patch_ListAddContentWithConfigration_AddsTheItem()
        {
            var originalTestPerson = new TestPerson();

            var patchRequest = new TestPersonRequest();
            patchRequest.PhoneNumbers.Add(new TestPhoneNumberRequest
            {
                PhoneNumber = "5678"
            });

            var json = @"{""PhoneNumbers"" : [{ ""PhoneNumber"" : ""5678"" }]}";

            var patchHelper = new PatchHelper(cfg =>
            {
                cfg.ForCollectionPath("PhoneNumbers").ChangeAddItem<TestPhoneNumberRequest, TestPhoneNumber>(x =>
                {
                    return new TestPhoneNumber();
                });

                return cfg;
            });

            var patchedTestPerson = await patchHelper.Patch<TestPerson>(originalTestPerson, patchRequest, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.First().PhoneNumber.Should().Be("5678");
        }

        /// <summary>
        /// Tests whether the patch method will patch a single and the right property 
        /// when called with the right parameters.
        /// </summary>
        [Fact]
        public async Task Patch_JSON_Employee()
        {
            var originalTestPerson = new Employee()
            {
               
            };

            var patchRequest = new UpdateEmployeeRequest()
            {
                Address = new AddressModel() { FirstName = "Test" }
            };

            var json = "{ \"address\": { \"firstName\": \"string\" }}";

            var patchHelper = new PatchHelper();

            var patchedTestPerson = await patchHelper.Patch(originalTestPerson, patchRequest, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.Address.FirstName.Should().Be("Test");
        }
    }
}
