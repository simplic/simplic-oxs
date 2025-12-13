using FluentAssertions;
using Simplic.OxS.ServiceDefinition;
using Xunit;

namespace Simplic.OxS.Server.Test
{
    public class ServiceDefinitionServiceGrpcTest
    {
        [Fact]
        public void GrpcDefinitions_CanBeInstantiated_ShouldWork()
        {
            // Arrange & Act
            var grpcDefinition = new GrpcDefinitions
            {
                Package = "test",
                Service = "test.TestService",
                ProtoFile = new byte[] { 1, 2, 3, 4 }
            };

            // Assert
            grpcDefinition.Package.Should().Be("test");
            grpcDefinition.Service.Should().Be("test.TestService");
            grpcDefinition.ProtoFile.Should().NotBeNull();
            grpcDefinition.ProtoFile.Should().HaveCount(4);
        }

        [Fact]
        public void ServiceObject_GrpcDefinitions_ShouldBeInitializable()
        {
            // Arrange
            var serviceObject = new ServiceObject
            {
                Name = "TestService",
                Version = "v1"
            };

            var grpcDef = new GrpcDefinitions
            {
                Package = "test",
                Service = "test.TestService",
                ProtoFile = new byte[] { 1, 2, 3 }
            };

            // Act
            serviceObject.GrpcDefinitions ??= new List<GrpcDefinitions>();
            serviceObject.GrpcDefinitions.Add(grpcDef);

            // Assert
            serviceObject.GrpcDefinitions.Should().NotBeNull();
            serviceObject.GrpcDefinitions.Should().HaveCount(1);
            serviceObject.GrpcDefinitions.First().Package.Should().Be("test");
            serviceObject.GrpcDefinitions.First().Service.Should().Be("test.TestService");
        }

        [Fact]
        public void ServiceObject_GrpcDefinitions_DefaultInitialization_ShouldWork()
        {
            // Arrange & Act
            var serviceObject = new ServiceObject();

            // Assert
            serviceObject.GrpcDefinitions.Should().NotBeNull();
            serviceObject.GrpcDefinitions.Should().BeEmpty();
        }
    }
}