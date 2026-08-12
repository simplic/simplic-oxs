using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Simplic.OxS.Exceptions;
using Simplic.OxS.Server.Exceptions;
using Simplic.OxS.Server.Exceptions.Handlers;
using System.Text.Json;

namespace Simplic.OxS.Server.Test
{
    public class ExceptionsTest
    {
        private sealed class TestException : Exception
        { }

        [UnpackException]
        private sealed class PackedException : Exception
        {
            public PackedException(Exception inner) : base(null, inner)
            { }
        }

        private sealed class FakeEnvironment(string environmentName) : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = environmentName;
            public string ApplicationName { get; set; } = "Test";
            public string ContentRootPath { get; set; } = string.Empty;
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        }

        private static DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Path = "/test";
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            var body = await reader.ReadToEndAsync();
            return JsonDocument.Parse(body).RootElement.Clone();
        }

        private static OxSExceptionHandler CreateOxSHandler()
            => new(NullLogger<OxSExceptionHandler>.Instance);

        private static FrameworkExceptionHandler CreateFrameworkHandler()
            => new(NullLogger<FrameworkExceptionHandler>.Instance);

        [Fact]
        public void ExceptionUnpacker_UnpackableChain_FindsTarget()
        {
            var thrown = new PackedException(new PackedException(new TestException()));

            var found = ExceptionUnpacker.TryUnpack<TestException>(thrown, out var target);

            found.Should().BeTrue();
            target.Should().BeOfType<TestException>();
        }

        [Fact]
        public void ExceptionUnpacker_UnpackableChain_NoTarget_ReturnsFalse()
        {
            var thrown = new PackedException(new PackedException(new Exception("test")));

            var found = ExceptionUnpacker.TryUnpack<TestException>(thrown, out var target);

            found.Should().BeFalse();
            target.Should().BeNull();
        }

        [Fact]
        public void ExceptionUnpacker_NotUnpackableChain_DoesNotDescend()
        {
            var thrown = new Exception("", new Exception("", new TestException()));

            var found = ExceptionUnpacker.TryUnpack<TestException>(thrown, out _);

            found.Should().BeFalse();
        }

        [Fact]
        public async Task OxSExceptionHandler_BadRequestException_ReturnsProblemDetails()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler().TryHandleAsync(context, new BadRequestException("invalid request"), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(400);
            context.Response.ContentType.Should().StartWith("application/problem+json");

            var body = await ReadBodyAsync(context);
            body.GetProperty("status").GetInt32().Should().Be(400);
            body.GetProperty("title").GetString().Should().Be("Bad Request");
            body.GetProperty("detail").GetString().Should().Be("invalid request");
            body.GetProperty("type").GetString().Should().Be("urn:simplic-oxs:problem:bad-request");
        }

        [Fact]
        public async Task OxSExceptionHandler_ConflictException_ReturnsProblemDetails()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler().TryHandleAsync(context, new ConflictException("already exists"), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(409);

            var body = await ReadBodyAsync(context);
            body.GetProperty("title").GetString().Should().Be("Conflict");
            body.GetProperty("detail").GetString().Should().Be("already exists");
        }

        [Fact]
        public async Task OxSExceptionHandler_ResourceNotFound_IncludesResourceExtension()
        {
            var context = CreateContext();
            var id = Guid.NewGuid();

            var handled = await CreateOxSHandler().TryHandleAsync(context, global::Simplic.OxS.Exceptions.ResourceNotFoundException.FromType<ExceptionsTest>(id), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(404);

            var body = await ReadBodyAsync(context);
            body.GetProperty("title").GetString().Should().Be("Not Found");
            body.GetProperty("type").GetString().Should().Be("urn:simplic-oxs:problem:not-found");
            body.GetProperty("resource").GetString().Should().Be($"{nameof(ExceptionsTest)}@{id}");
            body.GetProperty("resourceType").GetString().Should().Be(nameof(ExceptionsTest));
            body.GetProperty("resourceId").GetString().Should().Be(id.ToString());
        }

        [Fact]
        public async Task OxSExceptionHandler_ObsoleteShims_StillHandled()
        {
            var context = CreateContext();

#pragma warning disable CS0618 // obsolete shims are covered on purpose
            var badRequest = new Server.BadRequestException("bad");
            var notFound = Simplic.OxS.ResourceNotFoundException.FromType<ExceptionsTest>(Guid.NewGuid());
#pragma warning restore CS0618

            badRequest.Should().BeAssignableTo<global::Simplic.OxS.Exceptions.BadRequestException>();
            notFound.Should().BeAssignableTo<global::Simplic.OxS.Exceptions.ResourceNotFoundException>();

            var handled = await CreateOxSHandler().TryHandleAsync(context, badRequest, default);
            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task OxSExceptionHandler_UnpackableChain_HandlesOxSException()
        {
            var context = CreateContext();
            var thrown = new PackedException(new PackedException(new BadRequestException("wrapped")));

            var handled = await CreateOxSHandler().TryHandleAsync(context, thrown, default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(400);

            var body = await ReadBodyAsync(context);
            body.GetProperty("detail").GetString().Should().Be("wrapped");
        }

        [Fact]
        public async Task OxSExceptionHandler_NonOxSException_DoesNotHandle()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler().TryHandleAsync(context, new TestException(), default);

            handled.Should().BeFalse();
        }

        [Fact]
        public async Task OxSExceptionHandler_UnauthorizedException_SetsWwwAuthenticateHeader()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler().TryHandleAsync(context, new UnauthorizedException("no token"), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(401);
            context.Response.Headers["WWW-Authenticate"].ToString().Should().Be("Bearer");
        }

        [Fact]
        public async Task OxSExceptionHandler_BadRequestException_SingleFieldError_IncludesErrors()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler().TryHandleAsync(context, new BadRequestException("customer.name", "must not be empty"), default);

            handled.Should().BeTrue();

            var body = await ReadBodyAsync(context);
            var errors = body.GetProperty("errors").GetProperty("customer.name");
            errors.EnumerateArray().Select(e => e.GetString()).Should().Equal("must not be empty");
        }

        [Fact]
        public async Task OxSExceptionHandler_BadRequestException_MultipleFieldErrors_IncludesAll()
        {
            var context = CreateContext();
            var input = new Dictionary<string, string[]>
            {
                ["customer.name"] = new[] { "must not be empty", "too long" },
                ["customer.age"] = new[] { "must be positive" }
            };

            var handled = await CreateOxSHandler().TryHandleAsync(context, new BadRequestException(input), default);

            handled.Should().BeTrue();

            var body = await ReadBodyAsync(context);
            body.GetProperty("title").GetString().Should().Be("Bad Request");
            body.GetProperty("detail").GetString().Should().Be("One or more validation errors occurred.");

            var errors = body.GetProperty("errors");
            errors.GetProperty("customer.name").EnumerateArray().Select(e => e.GetString()).Should().Equal("must not be empty", "too long");
            errors.GetProperty("customer.age").EnumerateArray().Select(e => e.GetString()).Should().Equal("must be positive");
        }

        [Fact]
        public void BadRequestException_AddError_AccumulatesPerProperty()
        {
            var exception = new BadRequestException("customer.name", "must not be empty")
                .AddError("customer.name", "too long")
                .AddError("customer.age", "must be positive");

            var extensions = new Dictionary<string, object?>();
            exception.PopulateProblemDetails(extensions);

            var errors = (Dictionary<string, string[]>)extensions["errors"]!;
            errors["customer.name"].Should().Equal("must not be empty", "too long");
            errors["customer.age"].Should().Equal("must be positive");
        }

        [Fact]
        public async Task FrameworkExceptionHandler_BadHttpRequestTooLarge_Returns413()
        {
            var context = CreateContext();

            var handled = await CreateFrameworkHandler()
                .TryHandleAsync(context, new BadHttpRequestException("too large", StatusCodes.Status413PayloadTooLarge), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(413);

            var body = await ReadBodyAsync(context);
            body.GetProperty("status").GetInt32().Should().Be(413);
        }

        [Fact]
        public async Task FrameworkExceptionHandler_InvalidData_Returns413()
        {
            var context = CreateContext();

            var handled = await CreateFrameworkHandler()
                .TryHandleAsync(context, new InvalidDataException("multipart too large"), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(413);
        }

        [Fact]
        public async Task FrameworkExceptionHandler_ClientDisconnect_Returns499NoBody()
        {
            var context = CreateContext();
            context.RequestAborted = new CancellationToken(canceled: true);

            var handled = await CreateFrameworkHandler()
                .TryHandleAsync(context, new OperationCanceledException(), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(499);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            (await reader.ReadToEndAsync()).Should().BeEmpty();
        }

        [Fact]
        public async Task FrameworkExceptionHandler_UnknownException_DoesNotHandle()
        {
            var context = CreateContext();

            var handled = await CreateFrameworkHandler().TryHandleAsync(context, new TestException(), default);

            handled.Should().BeFalse();
        }

        [Fact]
        public async Task FallbackExceptionHandler_Production_ReturnsGeneric500()
        {
            var context = CreateContext();
            var handler = new FallbackExceptionHandler(new FakeEnvironment(Environments.Production), NullLogger<FallbackExceptionHandler>.Instance);

            var handled = await handler.TryHandleAsync(context, new TestException(), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(500);

            var body = await ReadBodyAsync(context);
            body.GetProperty("detail").GetString().Should().Be("An unexpected error occurred while processing the request.");
        }

        [Fact]
        public async Task FallbackExceptionHandler_Development_DoesNotHandle()
        {
            var context = CreateContext();
            var handler = new FallbackExceptionHandler(new FakeEnvironment(Environments.Development), NullLogger<FallbackExceptionHandler>.Instance);

            var handled = await handler.TryHandleAsync(context, new TestException(), default);

            handled.Should().BeFalse();
        }

        private sealed class DetailOverrideException : OxSException
        {
            public DetailOverrideException() : base("internal log-only message") { }

            public override int StatusCode => 400;

            public override string? Detail => "client-safe detail";
        }

        [Fact]
        public async Task OxSExceptionHandler_Detail_OverridesMessageInBody()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler().TryHandleAsync(context, new DetailOverrideException(), default);

            handled.Should().BeTrue();

            var body = await ReadBodyAsync(context);
            body.GetProperty("detail").GetString().Should().Be("client-safe detail");
        }

        [Fact]
        public async Task OxSExceptionHandler_NotFoundException_IsAnonymous()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler().TryHandleAsync(context, new NotFoundException(), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(404);

            var body = await ReadBodyAsync(context);
            body.GetProperty("title").GetString().Should().Be("Not Found");
            body.GetProperty("type").GetString().Should().Be("urn:simplic-oxs:problem:not-found");
            body.GetProperty("detail").GetString().Should().Be("The requested resource was not found.");
            body.TryGetProperty("resource", out _).Should().BeFalse();
            body.TryGetProperty("resourceId", out _).Should().BeFalse();
        }

        [Fact]
        public async Task OxSExceptionHandler_PayloadTooLarge_Returns413()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler().TryHandleAsync(context, new PayloadTooLargeException("too big"), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(413);

            var body = await ReadBodyAsync(context);
            body.GetProperty("type").GetString().Should().Be("urn:simplic-oxs:problem:payload-too-large");
        }

        [Fact]
        public async Task OxSExceptionHandler_UnprocessableEntity_IncludesErrors()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler()
                .TryHandleAsync(context, new UnprocessableEntityException("order.total", "must be positive"), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(422);

            var body = await ReadBodyAsync(context);
            body.GetProperty("type").GetString().Should().Be("urn:simplic-oxs:problem:unprocessable-entity");
            body.GetProperty("errors").GetProperty("order.total")
                .EnumerateArray().Select(e => e.GetString()).Should().Equal("must be positive");
        }

        [Fact]
        public async Task OxSExceptionHandler_TooManyRequests_SetsRetryAfterHeader()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler()
                .TryHandleAsync(context, new TooManyRequestsException("slow down", TimeSpan.FromSeconds(30)), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(429);
            context.Response.Headers["Retry-After"].ToString().Should().Be("30");
        }

        [Fact]
        public async Task OxSExceptionHandler_ServiceUnavailable_Returns503()
        {
            var context = CreateContext();

            var handled = await CreateOxSHandler()
                .TryHandleAsync(context, new ServiceUnavailableException("maintenance"), default);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(503);

            var body = await ReadBodyAsync(context);
            body.GetProperty("type").GetString().Should().Be("urn:simplic-oxs:problem:service-unavailable");
        }

        [Fact]
        public void ValidationActionFilter_InvalidModelState_ThrowsBadRequestException()
        {
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            modelState.AddModelError("customer.name", "must not be empty");
            modelState.AddModelError("customer.name", "too long");

            var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
                new DefaultHttpContext(),
                new Microsoft.AspNetCore.Routing.RouteData(),
                new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(),
                modelState);

            var executingContext = new Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext(
                actionContext,
                new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>(),
                new Dictionary<string, object?>(),
                controller: null!);

            var filter = new Server.Filter.ValidationActionFilter();

            var act = () => filter.OnActionExecuting(executingContext);

            var exception = act.Should().Throw<BadRequestException>().Which;

            var extensions = new Dictionary<string, object?>();
            exception.PopulateProblemDetails(extensions);
            var errors = (Dictionary<string, string[]>)extensions["errors"]!;
            errors["customer.name"].Should().Equal("must not be empty", "too long");
        }
    }
}
