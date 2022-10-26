using FluentAssertions;

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

            var patchHelper = new PatchHelper();

            var patchedTestPerson = patchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
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
        public void Patch_SingleFieldJson_PatchesSingleField_IgnoreCase()
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

            // Use lower-case property
            var json = @"{""lastName"" : ""Doe""}";

            var patchHelper = new PatchHelper();

            var patchedTestPerson = patchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
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

            var patchHelper = new PatchHelper();

            var patchedTestPerson = patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
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

            var patchHelper = new PatchHelper();

            var patchedTestPerson = patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
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

            var patchHelper = new PatchHelper();

            var patchedTestPerson = patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
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

            var patchHelper = new PatchHelper();

            var patchedTestPerson = patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
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

            var patchHelper = new PatchHelper();

            var patchedTestPerson = patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.Count().Should().Be(0);
        }

        /// <summary>
        /// Tests whether the patch method will add a new item to the original object.
        /// </summary>
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

            var patchHelper = new PatchHelper();

            var patchedTestPerson = patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return true;
            });

            patchedTestPerson.PhoneNumbers.First().PhoneNumber.Should().Be("5678");
        }

        /// <summary>
        /// Tests whether the patch method will throw an exception when the validation request returns false.
        /// </summary>
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

            var patchHelper = new PatchHelper();

            this.Invoking(x => patchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                return false;
            })).Should().Throw<BadRequestException>();
        }

        /// <summary>
        /// Tests whether the patch method will set the property name in validation requests. 
        /// </summary>
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

            var patchHelper = new PatchHelper();

            this.Invoking(x => patchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                if (validation.Property == "LastName")
                    return false;

                return true;
            })).Should().Throw<BadRequestException>();
        }

        /// <summary>
        /// Tests whether the patch method will set the value in validation requests. 
        /// </summary>
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

            var patchHelper = new PatchHelper();

            this.Invoking(x => patchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                if (validation.Value == "Doe")
                    return false;

                return true;
            })).Should().Throw<BadRequestException>();
        }

        /// <summary>
        /// Tests whether the patch method will set the path in validation requests. 
        /// </summary>
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

            var patchHelper = new PatchHelper();

            this.Invoking(x => patchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                if (validation.Path == "LastName")
                    return false;

                return true;
            })).Should().Throw<BadRequestException>();
        }

        /// <summary>
        /// Tests whether the patch method will throw a bad request exception when a property is contained 
        /// in the json but is not part of the object.
        /// </summary>
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

            var patchHelper = new PatchHelper();

            this.Invoking(x => patchHelper.Patch(originalTestPerson, mappedTestPerson, json, (validation) =>
            {
                if (validation.Path == "LastName")
                    return false;

                return true;
            })).Should().Throw<BadRequestException>();
        }

        /// <summary>
        /// Test whether validation returns always truem if no validation (null) is passed
        /// </summary>
        [Fact]
        public void Patch_AllFieldJson_ValidateAll()
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

            var patchHelper = new PatchHelper();

            var patchedTestPerson = patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, null);

            patchedTestPerson.FirstName.Should().Be("John");
            patchedTestPerson.LastName.Should().Be("Doe");
        }

        /// <summary>
        /// Check whether an inner exception is passed for invalid json
        /// </summary>
        [Fact]
        public void Patch_CatchInvalidJsonException()
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
                        this is invalid ....";

            var patchHelper = new PatchHelper();

            this.Invoking(x => patchHelper.Patch(originalTestPerson, mappedTestPerson, json, null))
                .Should().Throw<ArgumentException>()
                .WithInnerException<System.Text.Json.JsonException>();
        }


        /// <summary>
        /// Test whether patch will apply changes properly
        /// </summary>
        [Fact]
        public void Patch_AllFieldJson_Configuration()
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

            var patchHelper = new PatchHelper(cfg =>
            {
                cfg.ForPath("FirstName").Change<TestPerson, TestPerson>((original, patch) => { original.FirstName = "Peter"; return true; });
                return cfg;
            });

            var patchedTestPerson = patchHelper.Patch<TestPerson>(originalTestPerson, mappedTestPerson, json, null);

            patchedTestPerson.FirstName.Should().Be("Peter");
            patchedTestPerson.LastName.Should().Be("Doe");
        }

    }
}
