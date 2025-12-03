using MassTransit.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.ServiceDefinition;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Test.Extensions;

public class RemoteServiceExtensionTests
{
    private readonly RemoteServiceInvoker _invoker;

    public RemoteServiceExtensionTests()
    {
        // Create mock dependencies for the constructor
        var mockCache = new Mock<IDistributedCache>();
        var mockRequestContext = new Mock<IRequestContext>();
        var mockEndpointContract = new Mock<IEndpointContractRepository>();
        var mockSettings = new Mock<IOptions<AuthSettings>>();

        _invoker = new RemoteServiceInvoker(mockCache.Object, mockEndpointContract.Object, mockSettings.Object, mockRequestContext.Object);
    }

    private bool InvokeTryParseProtocol(string uri, out string? protocol, out string? url)
    {
        // Use reflection to access the private method
        var method = typeof(RemoteServiceInvoker).GetMethod("TryParseProtocol",
     BindingFlags.NonPublic | BindingFlags.Instance);

        var parameters = new object[] { uri, null!, null! };
        var result = (bool)method!.Invoke(_invoker, parameters)!;

        protocol = parameters[1] as string;
        url = parameters[2] as string;

        return result;
    }

    [Fact]
    public void TryParseProtocol_WithValidGrpcUri_ShouldReturnTrue()
    {
        // Arrange
        var uri = "[grpc]https://example.com::MyService::MyMethod";

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeTrue();
        protocol.Should().Be("grpc");
        url.Should().Be("https://example.com::MyService::MyMethod");
    }

    [Fact]
    public void TryParseProtocol_WithGrpcUriAndExtraSpaces_ShouldTrimUrl()
    {
        // Arrange
        var uri = "[grpc]   https://example.com::MyService::MyMethod   ";

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeTrue();
        protocol.Should().Be("grpc");
        url.Should().Be("https://example.com::MyService::MyMethod");
    }

    [Fact]
    public void TryParseProtocol_WithInvalidProtocol_ShouldReturnFalse()
    {
        // Arrange
        var uri = "[invalid]https://example.com";

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeFalse();
        protocol.Should().BeNull();
        url.Should().BeNull();
    }

    [Fact]
    public void TryParseProtocol_WithNoProtocolPrefix_ShouldReturnFalse()
    {
        // Arrange
        var uri = "https://example.com";

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeFalse();
        protocol.Should().BeNull();
        url.Should().BeNull();
    }

    [Fact]
    public void TryParseProtocol_WithEmptyString_ShouldReturnFalse()
    {
        // Arrange
        var uri = "";

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeFalse();
        protocol.Should().BeNull();
        url.Should().BeNull();
    }

    [Fact]
    public void TryParseProtocol_WithNullString_ShouldThrow()
    {
        // Arrange
        string uri = null!;

        // Act & Assert
        var act = () => InvokeTryParseProtocol(uri, out string? protocol, out string? url);
        act.Should().Throw<TargetInvocationException>();
    }

    [Fact]
    public void TryParseProtocol_WithMalformedGrpcProtocol_ShouldReturnFalse()
    {
        // Arrange
        var uri = "[grpc https://example.com"; // Missing closing bracket

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeFalse();
        protocol.Should().BeNull();
        url.Should().BeNull();
    }

    [Fact]
    public void TryParseProtocol_WithMalformedHttpPostProtocol_ShouldReturnFalse()
    {
        // Arrange
        var uri = "[http.post https://example.com"; // Missing closing bracket

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeFalse();
        protocol.Should().BeNull();
        url.Should().BeNull();
    }

    [Fact]
    public void TryParseProtocol_WithOnlyProtocolPrefix_ShouldReturnTrueWithEmptyUrl()
    {
        // Arrange
        var uri = "[grpc]";

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeTrue();
        protocol.Should().Be("grpc");
        url.Should().Be("");
    }

    [Fact]
    public void TryParseProtocol_WithCaseVariation_ShouldReturnFalse()
    {
        // Arrange
        var uri = "[GRPC]https://example.com"; // Case sensitive

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeFalse();
        protocol.Should().BeNull();
        url.Should().BeNull();
    }

    [Fact]
    public void TryParseProtocol_WithWhitespaceOnlyAfterPrefix_ShouldReturnTrueWithEmptyUrl()
    {
        // Arrange
        var uri = "[grpc]   ";

        // Act
        var result = InvokeTryParseProtocol(uri, out string? protocol, out string? url);

        // Assert
        result.Should().BeTrue();
        protocol.Should().Be("grpc");
        url.Should().Be("");
    }
}