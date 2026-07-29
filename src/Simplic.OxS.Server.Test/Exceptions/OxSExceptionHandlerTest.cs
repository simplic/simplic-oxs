using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Simplic.OxS.Server.Exceptions;

namespace Simplic.OxS.Server.Test.Exceptions;

/// <summary>
/// Guards the fleet-wide error contract: every failure becomes an RFC 7807
/// <see cref="ProblemDetails"/> with a stable <c>errorCode</c> and a <c>correlationId</c> that is
/// also present in the logs.
/// </summary>
public class OxSExceptionHandlerTest
{
    private const string ProductionEnvironment = "Production";

    /// <summary>Exception carrying a custom contract, to prove services can extend the mapping.</summary>
    private sealed class CustomDomainException(string message) : Exception(message), IOxSException
    {
        public int StatusCode => StatusCodes.Status418ImATeapot;
        public string ErrorCode => "custom_domain_error";

        public IReadOnlyDictionary<string, object?> ProblemExtensions
            => new Dictionary<string, object?> { ["custom"] = "value" };
    }

    [UnpackException]
    private sealed class PackedException(Exception inner) : Exception(null, inner);

    private static (OxSExceptionHandler Handler, HttpContext Context, Func<ProblemDetails?> Captured) CreateSut(
        string environmentName = ProductionEnvironment,
        Guid? correlationId = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new Mock<IServiceProvider>().Object,
        };
        context.Request.Method = "GET";
        context.Request.Path = "/shipment/abc";
        context.Request.Headers[Constants.HttpHeaderCorrelationIdKey] =
            (correlationId ?? Guid.NewGuid()).ToString();

        ProblemDetails? captured = null;

        var problemDetailsService = new Mock<IProblemDetailsService>();
        problemDetailsService
            .Setup(x => x.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => captured = ctx.ProblemDetails)
            .Returns(ValueTask.FromResult(true));

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);

        var handler = new OxSExceptionHandler(
            NullLogger<OxSExceptionHandler>.Instance,
            problemDetailsService.Object,
            environment.Object);

        return (handler, context, () => captured);
    }

    [Fact]
    public async Task TryHandleAsync_ResourceNotFound_Returns404WithResourceMembers()
    {
        var id = Guid.NewGuid();
        var (handler, context, captured) = CreateSut();

        var handled = await handler.TryHandleAsync(
            context, new ResourceNotFoundException("Shipment", id), CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var problem = captured();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Extensions["errorCode"].Should().Be("resource_not_found");
        problem.Extensions["resourceType"].Should().Be("Shipment");
        problem.Extensions["resourceId"].Should().Be(id.ToString());
    }

    [Fact]
    public async Task TryHandleAsync_BadRequest_Returns400AndExposesTheMessage()
    {
        var (handler, context, captured) = CreateSut();

        await handler.TryHandleAsync(
            context, new BadRequestException("Cannot approve a cancelled shipment."), CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problem = captured();
        problem!.Extensions["errorCode"].Should().Be("bad_request");
        problem.Title.Should().Be("Cannot approve a cancelled shipment.");
    }

    [Fact]
    public async Task TryHandleAsync_Conflict_Returns409()
    {
        var (handler, context, captured) = CreateSut();

        await handler.TryHandleAsync(
            context, new ConflictException("Modified by another user."), CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        captured()!.Extensions["errorCode"].Should().Be("conflict");
    }

    [Fact]
    public async Task TryHandleAsync_Forbidden_Returns403()
    {
        var (handler, context, captured) = CreateSut();

        await handler.TryHandleAsync(context, new ForbiddenException("Not your org."), CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        captured()!.Extensions["errorCode"].Should().Be("forbidden");
    }

    [Fact]
    public async Task TryHandleAsync_CustomDomainException_UsesItsOwnContract()
    {
        var (handler, context, captured) = CreateSut();

        await handler.TryHandleAsync(context, new CustomDomainException("teapot"), CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status418ImATeapot);

        var problem = captured();
        problem!.Extensions["errorCode"].Should().Be("custom_domain_error");
        problem.Extensions["custom"].Should().Be("value");
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_Returns500AndDoesNotLeakTheMessage()
    {
        var (handler, context, captured) = CreateSut();

        await handler.TryHandleAsync(
            context, new InvalidOperationException("connection string sa:hunter2"), CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problem = captured();
        problem!.Extensions["errorCode"].Should().Be("unhandled");
        problem.Title.Should().NotContain("hunter2");
        problem.Extensions.Should().NotContainKey("stackTrace", "detail must never leak outside development");
        problem.Extensions.Should().NotContainKey("exceptionMessage");
    }

    [Fact]
    public async Task TryHandleAsync_InDevelopment_IncludesExceptionDetail()
    {
        var (handler, context, captured) = CreateSut(environmentName: "Development");

        await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        var problem = captured();
        problem!.Extensions["exceptionMessage"].Should().Be("boom");
        problem.Extensions.Should().ContainKey("stackTrace");
        problem.Extensions["exceptionType"].Should().Be(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task TryHandleAsync_AlwaysReportsTheRequestCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        var (handler, context, captured) = CreateSut(correlationId: correlationId);

        await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        captured()!.Extensions["correlationId"].Should().Be(correlationId.ToString(),
            "the caller must be able to quote an id that is findable in the logs");
    }

    [Fact]
    public async Task TryHandleAsync_UnpackableWrapper_ResolvesTheInnerDomainException()
    {
        var (handler, context, captured) = CreateSut();
        var wrapped = new PackedException(new PackedException(new ConflictException("inner conflict")));

        await handler.TryHandleAsync(context, wrapped, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        captured()!.Extensions["errorCode"].Should().Be("conflict");
    }

    [Fact]
    public async Task TryHandleAsync_PlainWrapper_StillFindsTheDomainException()
    {
        // A domain exception wrapped by a mapper or similar must not degrade to a 500 just
        // because the wrapper is not annotated as unpackable.
        var (handler, context, captured) = CreateSut();
        var wrapped = new InvalidOperationException("mapping failed", new ResourceNotFoundException("Tour", 7));

        await handler.TryHandleAsync(context, wrapped, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        captured()!.Extensions["errorCode"].Should().Be("resource_not_found");
    }

    [Fact]
    public async Task TryHandleAsync_ClientCancelled_IsHandledWithoutWritingAResponse()
    {
        var (handler, context, captured) = CreateSut();

        var handled = await handler.TryHandleAsync(
            context, new OperationCanceledException(), CancellationToken.None);

        handled.Should().BeTrue("the exception is accounted for, so it must not be rethrown");
        captured().Should().BeNull("no body can be written once the client has gone");
    }
}
