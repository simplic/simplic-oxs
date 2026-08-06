using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Simplic.OxS.Exceptions;
using Simplic.OxS.Server.Exceptions;

namespace Simplic.OxS.Server.Test
{
    public class ExceptionsTest
    {
        class TestException : Exception
        { }

        class PlainBodyException : OxSException
        {
            public PlainBodyException(string message) : base(message) { }

            public override int StatusCode => 400;

            public override bool IncludeProblemDetails => false;
        }

        class TestExceptionFilterAttribute : CommonExceptionFilterAttribute<TestException>
        {
            protected override void HandleException(ExceptionContext context, TestException exception)
            {
                context.Result = new OkObjectResult("test");
            }
        }

        [UnpackException]
        class PackedException : Exception
        {
            public PackedException(Exception inner) : base(null, inner)
            { }
        }

        [Fact]
        public async Task CommonExceptionFilterAttribute_UnpackableChain_ValidTarget_Handled()
        {
            var filter = new TestExceptionFilterAttribute();

            var thrownException = new PackedException(new PackedException(new TestException()));

            var context = new ExceptionContext(new ActionContext(new DefaultHttpContext(), new(), new()), [])
            {
                Exception = thrownException,
            };

            await filter.OnExceptionAsync(context);

            context.Result.Should().NotBeNull().And.BeAssignableTo<OkObjectResult>();

            var okResult = (OkObjectResult)context.Result;
            okResult.Value.Should().Be("test");
        }

        [Fact]
        public async Task CommonExceptionFilterAttribute_UnpackableChain_InvalidTarget_DoesNotHandle()
        {
            var filter = new TestExceptionFilterAttribute();

            var thrownException = new PackedException(new PackedException(new Exception("test")));

            var context = new ExceptionContext(new ActionContext(new DefaultHttpContext(), new(), new()), [])
            {
                Exception = thrownException,
            };

            await filter.OnExceptionAsync(context);

            context.Result.Should().BeNull();
        }

        [Fact]
        public async Task CommonExceptionFilterAttribute_NotUnpackableChain_DoesNotHandle()
        {
            var filter = new TestExceptionFilterAttribute();

            var thrownException = new Exception("", new Exception("", new TestException()));

            var context = new ExceptionContext(new ActionContext(new DefaultHttpContext(), new(), new()), [])
            {
                Exception = thrownException,
            };

            await filter.OnExceptionAsync(context);

            context.Result.Should().BeNull();
        }

        private static ExceptionContext CreateContext(Exception exception)
        {
            return new ExceptionContext(new ActionContext(new DefaultHttpContext(), new(), new()), [])
            {
                Exception = exception,
            };
        }

        private static ProblemDetails AssertProblemDetails(ExceptionContext context, int expectedStatus)
        {
            context.Result.Should().NotBeNull().And.BeAssignableTo<ObjectResult>();

            var objectResult = (ObjectResult)context.Result!;
            objectResult.StatusCode.Should().Be(expectedStatus);
            objectResult.Value.Should().BeAssignableTo<ProblemDetails>();

            return (ProblemDetails)objectResult.Value!;
        }

        [Fact]
        public async Task OxSExceptionFilter_BadRequestException_ReturnsProblemDetails()
        {
            var filter = new OxSExceptionFilterAttribute();
            var context = CreateContext(new BadRequestException("invalid request"));

            await filter.OnExceptionAsync(context);

            var problemDetails = AssertProblemDetails(context, 400);
            problemDetails.Status.Should().Be(400);
            problemDetails.Title.Should().Be("Bad Request");
            problemDetails.Detail.Should().Be("invalid request");
            problemDetails.Type.Should().Be("about:blank");
        }

        [Fact]
        public async Task OxSExceptionFilter_ConflictException_ReturnsProblemDetails()
        {
            var filter = new OxSExceptionFilterAttribute();
            var context = CreateContext(new ConflictException("already exists"));

            await filter.OnExceptionAsync(context);

            var problemDetails = AssertProblemDetails(context, 409);
            problemDetails.Title.Should().Be("Conflict");
            problemDetails.Detail.Should().Be("already exists");
        }

        [Fact]
        public async Task OxSExceptionFilter_ResourceNotFound_IncludesResourceExtension()
        {
            var filter = new OxSExceptionFilterAttribute();
            var id = Guid.NewGuid();
            var context = CreateContext(ResourceNotFoundException.FromType<ExceptionsTest>(id));

            await filter.OnExceptionAsync(context);

            var problemDetails = AssertProblemDetails(context, 404);
            problemDetails.Title.Should().Be("Not Found");
            problemDetails.Extensions.Should().ContainKey("resource");
            problemDetails.Extensions["resource"].Should().Be($"{nameof(ExceptionsTest)}@{id}");
            problemDetails.Extensions["resourceType"].Should().Be(nameof(ExceptionsTest));
            problemDetails.Extensions["resourceId"].Should().Be(id);
        }

        [Fact]
        public async Task OxSExceptionFilter_IncludeProblemDetailsFalse_ReturnsPlainBody()
        {
            var filter = new OxSExceptionFilterAttribute();
            var context = CreateContext(new PlainBodyException("plain message"));

            await filter.OnExceptionAsync(context);

            context.Result.Should().NotBeNull().And.BeAssignableTo<ObjectResult>();

            var objectResult = (ObjectResult)context.Result!;
            objectResult.StatusCode.Should().Be(400);
            objectResult.Value.Should().Be("plain message");
        }

        [Fact]
        public async Task OxSExceptionFilter_UnpackableChain_HandlesOxSException()
        {
            var filter = new OxSExceptionFilterAttribute();
            var context = CreateContext(new PackedException(new PackedException(new BadRequestException("wrapped"))));

            await filter.OnExceptionAsync(context);

            var problemDetails = AssertProblemDetails(context, 400);
            problemDetails.Detail.Should().Be("wrapped");
        }

        [Fact]
        public async Task OxSExceptionFilter_BadRequestException_SingleFieldError_IncludesErrorsExtension()
        {
            var filter = new OxSExceptionFilterAttribute();
            var context = CreateContext(new BadRequestException("customer.name", "must not be empty"));

            await filter.OnExceptionAsync(context);

            var problemDetails = AssertProblemDetails(context, 400);
            problemDetails.Extensions.Should().ContainKey("errors");

            var errors = (Dictionary<string, string[]>)problemDetails.Extensions["errors"]!;
            errors.Should().ContainKey("customer.name");
            errors["customer.name"].Should().Equal("must not be empty");
        }

        [Fact]
        public async Task OxSExceptionFilter_BadRequestException_MultipleFieldErrors_IncludesAll()
        {
            var filter = new OxSExceptionFilterAttribute();
            var input = new Dictionary<string, string[]>
            {
                ["customer.name"] = new[] { "must not be empty", "too long" },
                ["customer.age"] = new[] { "must be positive" }
            };
            var context = CreateContext(new BadRequestException(input));

            await filter.OnExceptionAsync(context);

            var problemDetails = AssertProblemDetails(context, 400);
            problemDetails.Title.Should().Be("Bad Request");
            problemDetails.Detail.Should().Be("One or more validation errors occurred.");

            var errors = (Dictionary<string, string[]>)problemDetails.Extensions["errors"]!;
            errors["customer.name"].Should().Equal("must not be empty", "too long");
            errors["customer.age"].Should().Equal("must be positive");
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
    }
}
