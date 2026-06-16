using FluentAssertions;
using Simplic.OxS.ModelDefinition.Extenstion.Abstractions;
using Simplic.OxS.ModelDefinition.Service;
using Simplic.OxS.ModelDefinitionExtension.Test.TestEnv;
using System.ComponentModel.DataAnnotations;

namespace Simplic.OxS.ModelDefinitionExtension.Test
{
    public class ModelDefinitionServiceTests
    {
        [Fact]
        public void GenerateDefinitionForController_WithGetOperationAttribute_BuildsGetOperation()
        {
            // Arrange
            var controllerType = typeof(TestController);

            // Act
            var modelDefinition = ModelDefinitionService.GenerateDefinitionForController(controllerType);

            // Assert
            modelDefinition.Operations.Get.Should().NotBeNull();
            modelDefinition.Operations.Get.Endpoint.Should().Be("/test/get");
            modelDefinition.Operations.Get.ResponseReference.Should().Be("$TestResponse");
            modelDefinition.Operations.Get.Type.Should().Be("http-get");
        }

        [Fact]
        public void GenerateDefinitionForController_WithPostOperationAttribute_BuildsPostOperation()
        {
            // Arrange
            var controllerType = typeof(TestController);

            // Act
            var modelDefinition = ModelDefinitionService.GenerateDefinitionForController(controllerType);

            // Assert
            modelDefinition.Operations.Create.Should().NotBeNull();
            modelDefinition.Operations.Create.Endpoint.Should().Be("/test/post");
            modelDefinition.Operations.Create.RequestReference.Should().Be("$TestRequest");
            modelDefinition.Operations.Create.ResponseReference.Should().Be("$TestResponse");
            modelDefinition.Operations.Create.Type.Should().Be("http.post");
        }

        [Fact]
        public void GenerateDefinitionForController_WithPatchOperationAttribute_BuildsPatchOperation()
        {
            // Arrange
            var controllerType = typeof(TestController);

            // Act
            var modelDefinition = ModelDefinitionService.GenerateDefinitionForController(controllerType);

            // Assert
            modelDefinition.Operations.Update.Should().NotBeNull();
            modelDefinition.Operations.Update.Endpoint.Should().Be("/test/patch");
            modelDefinition.Operations.Update.RequestReference.Should().Be("$TestRequest");
            modelDefinition.Operations.Update.ResponseReference.Should().Be("$TestResponse");
            modelDefinition.Operations.Update.Type.Should().Be("http-patch");
        }

        [Fact]
        public void GenerateDefinitionForController_WithDeleteOperationAttribute_BuildsDeleteOperation()
        {
            // Arrange
            var controllerType = typeof(TestController);

            // Act
            var modelDefinition = ModelDefinitionService.GenerateDefinitionForController(controllerType);

            // Assert
            modelDefinition.Operations.Delete.Should().NotBeNull();
            modelDefinition.Operations.Delete.Endpoint.Should().Be("/test/delete");
            modelDefinition.Operations.Delete.Type.Should().Be("http-delete");
        }

        [Fact]
        public void GenerateDefinitionForController_WithoutOperations_BuildsEmptyModelDefinition()
        {
            // Arrange
            var controllerType = typeof(EmptyController);

            // Act
            var modelDefinition = ModelDefinitionService.GenerateDefinitionForController(controllerType);

            // Assert
            modelDefinition.Operations.Get.Should().BeNull();
            modelDefinition.Operations.Create.Should().BeNull();
            modelDefinition.Operations.Update.Should().BeNull();
            modelDefinition.Operations.Delete.Should().BeNull();
        }

        [Fact]
        public void GenerateDefinitionForController_WithAttributes_BuildsCorrectAttributes()
        {
            // Arrange
            var controllerType = typeof(TestController);

            // Act
            var modelDefinition = ModelDefinitionService.GenerateDefinitionForController(controllerType);

            // Assert
            // Verify properties in TestRequest
            var requestProperties = modelDefinition.References.First(x => x.Model == "$TestRequest").Properties;
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.Id)));
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.Name)) && p.Required);
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.Description)) && p.MaxValue == "50");
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.Age)) && p.MinValue == "0" && p.MaxValue == "100");
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.PhoneNumber)) && p.Format == "phone-number");
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.Email)) && p.Format == "email");
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.FileName)) && p.Format == "file-extension");
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.NestedObject)) && p.Type == "$NestedObject");
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.NestedObjects)) && p.ArrayType == "$NestedObject");
            requestProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestRequest.Status)) && p.EnumType == "int");

            // Verify properties in TestResponse
            var responseProperties = modelDefinition.Properties;
            responseProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestResponse.Id)));
            responseProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestResponse.Name)) && p.Required);
            responseProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestResponse.Description)) && p.MaxValue == "100");
            responseProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestResponse.Quantity)) && p.MinValue == "1" && p.MaxValue == "200");
            responseProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestResponse.NestedObject)) && p.Type == "$NestedObject");
            responseProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestResponse.NestedObjects)) && p.ArrayType == "$NestedObject");
            responseProperties.Should().ContainSingle(p => p.Name == ToCamelCase(nameof(TestResponse.Status)) && p.EnumType == "int");
        }


        [Fact]
        public void GenerateDefinitionForController_WithAttributes_AddsDataSource()
        {
            // Arrange
            var controllerType = typeof(TestController);

            // Act
            var modelDefinition = ModelDefinitionService.GenerateDefinitionForController(controllerType);


            modelDefinition.DataSources.Should().HaveCount(1);
            modelDefinition.DataSources.First().Type.Should().Be(ModelDefinition.DataSourceType.GraphQL);
        }

        [Fact]
        public void GenerateDefinitionForController_WithCircularAvailableTypeReferences_DoesNotRecurseInfinitely()
        {
            var modelDefinition = ModelDefinitionService.GenerateDefinitionForController(typeof(CircularController));

            modelDefinition.Operations.Get.Should().NotBeNull();
            modelDefinition.Operations.Get.ResponseReference.Should().Be("$CircularResponse");

            modelDefinition.References.Count(x => x.Model == "$CircularTypeA").Should().Be(1);
            modelDefinition.References.Count(x => x.Model == "$CircularTypeB").Should().Be(1);

            var typeAProperty = modelDefinition.References.First(x => x.Model == "$CircularTypeA").Properties.First();
            typeAProperty.AvailableTypes.Should().Contain("$CircularTypeB");
            typeAProperty.MaxValue.Should().Be("10");

            var typeBProperty = modelDefinition.References.First(x => x.Model == "$CircularTypeB").Properties.First();
            typeBProperty.AvailableTypes.Should().Contain("$CircularTypeA");
            typeBProperty.MaxValue.Should().Be("15");
        }


        private class EmptyController
        {

        }

        private class CircularController
        {
            [ModelDefinitionGetOperation("/circular/get", typeof(CircularResponse))]
            public void Get() { }
        }

        private class CircularResponse
        {
            [AvailableType(typeof(CircularTypeA))]
            public string Selector { get; set; } = string.Empty;
        }

        private class CircularTypeA
        {
            [AvailableType(typeof(CircularTypeB))]
            [StringLength(10)]
            public string Name { get; set; } = string.Empty;
        }

        private class CircularTypeB
        {
            [AvailableType(typeof(CircularTypeA))]
            [StringLength(15)]
            public string Name { get; set; } = string.Empty;
        }

        private static string ToCamelCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return char.ToLower(input[0]) + input.Substring(1);
        }
    }
}
