using FluentAssertions;
using Simplic.OxS.Server.Service;
using Xunit;

namespace Simplic.OxS.Server.Test
{
    public class ServiceDefinitionServiceTest
    {
        [Fact]
        public void ServiceDefinitionService_Constructor_ShouldInitializeProperties()
        {
            // Arrange & Act
            var service = new ServiceDefinitionService
            {
                ServiceName = "TestService",
                Version = "v1"
            };

            // Assert
            service.ServiceName.Should().Be("TestService");
            service.Version.Should().Be("v1");
        }

        [Fact]
        public void ServiceDefinitionService_Properties_ShouldBeSettable()
        {
            // Arrange
            var service = new ServiceDefinitionService();

            // Act
            service.ServiceName = "MyService";
            service.Version = "v2";

            // Assert
            service.ServiceName.Should().Be("MyService");
            service.Version.Should().Be("v2");
        }
    }
}