using Castle.Core.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Service;
using Xunit;

namespace Simplic.OxS.Server.Test
{
    public class ServiceDefinitionServiceTest
    {
        [Fact]
        public void ServiceDefinitionService_Constructor_ShouldInitializeProperties()
        {
            var logger = new Mock<ILogger<ServiceDefinitionService>>().Object;
            var serviceProvider = new Mock<IServiceProvider>().Object;

            // Arrange & Act
            var service = new ServiceDefinitionService(serviceProvider, logger)
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
            var logger = new Mock<ILogger<ServiceDefinitionService>>().Object;
            var serviceProvider = new Mock<IServiceProvider>().Object;

            // Arrange & Act
            var service = new ServiceDefinitionService(serviceProvider, logger);

            // Act
            service.ServiceName = "MyService";
            service.Version = "v2";

            // Assert
            service.ServiceName.Should().Be("MyService");
            service.Version.Should().Be("v2");
        }
    }
}