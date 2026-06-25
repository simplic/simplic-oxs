using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Simplic.OxS.Server.Exceptions;

namespace Simplic.OxS.Server.Test
{
    public class ExceptionsTest
    {
        class TestException : Exception
        { }

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
        public async Task CommonExceptionFilterAttribute_UnpacksException()
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
    }
}
