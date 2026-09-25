using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxQL.AspNetCore;
using OxQL.AspNetCore.Batch;
using OxQL.Core.Engine;
using OxQL.Core.Models;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>The internal batch route: admitted by the internal api key only, executed by the shared query service.</summary>
    public sealed class OxQLInternalControllerTests
    {
        private const string Key = "0e46ea43-6b5e-4b31-8008-95df146cf97d";

        private static OxQLInternalController Controller(IOxQLQueryService service) =>
            new(service, NullLogger<OxQLInternalController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

        private static ActionExecutingContext Executing(object controller, string? authorization)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IOptions<AuthSettings>>(Options.Create(new AuthSettings { InternalApiKey = Key }));

            var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

            if (authorization is not null)
                httpContext.Request.Headers.Authorization = authorization;

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

            return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller);
        }

        [Fact]
        public void Route_IsTheInternalBatchRouteAndHiddenFromTheExplorer()
        {
            typeof(OxQLInternalController).Should().BeDerivedFrom<OxSInternalController>();
            typeof(OxQLInternalController).GetCustomAttributes(typeof(RouteAttribute), false)
                .Cast<RouteAttribute>().Single().Template.Should().Be("internal/oxql");
            typeof(OxQLInternalController).GetMethod(nameof(OxQLInternalController.BatchAsync))!
                .GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>().Single().Template.Should().Be("batch");
            typeof(OxQLInternalController).GetCustomAttributes(typeof(ApiExplorerSettingsAttribute), false)
                .Cast<ApiExplorerSettingsAttribute>().Single().IgnoreApi.Should().BeTrue();
            typeof(OxQLInternalController).GetCustomAttributes(typeof(AuthorizeInternalApiKeyAttribute), true).Should().NotBeEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Bearer token")]
        [InlineData("i-api-key wrong")]
        public async Task InternalKeyAttribute_RefusesEverythingButTheKey(string? authorization)
        {
            var context = Executing(Controller(Mock.Of<IOxQLQueryService>()), authorization);
            var reached = false;

            await new AuthorizeInternalApiKeyAttribute().OnActionExecutionAsync(context, () =>
            {
                reached = true;
                return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
            });

            reached.Should().BeFalse();
            context.Result.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public async Task InternalKeyAttribute_AdmitsTheKeyOnThisController()
        {
            var context = Executing(Controller(Mock.Of<IOxQLQueryService>()), $"i-api-key {Key}");
            var reached = false;

            await new AuthorizeInternalApiKeyAttribute().OnActionExecutionAsync(context, () =>
            {
                reached = true;
                return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
            });

            reached.Should().BeTrue();
            context.Result.Should().BeNull();
        }

        [Fact]
        public async Task Batch_AnswersTheQueryServicesResponse()
        {
            var batch = new BatchRequest { Queries = [] };
            var response = new BatchResponse { Results = [new JsonObject { ["items"] = new JsonArray() }] };
            var service = new Mock<IOxQLQueryService>(MockBehavior.Strict);
            service.Setup(s => s.BatchAsync(batch, It.IsAny<CancellationToken>())).ReturnsAsync(new BatchOutcome.Success(response));

            var answer = await Controller(service.Object).BatchAsync(batch, CancellationToken.None);

            answer.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
        }

        [Fact]
        public async Task Batch_AnswersARefusalWithItsStatus()
        {
            var batch = new BatchRequest { Queries = [] };
            var refusal = Refusal.Validation([new QueryValidationError { Code = "BATCH_TOO_LARGE", Message = "too many" }]);
            var service = new Mock<IOxQLQueryService>(MockBehavior.Strict);
            service.Setup(s => s.BatchAsync(batch, It.IsAny<CancellationToken>())).ReturnsAsync(new BatchOutcome.Refused(refusal));

            var answer = await Controller(service.Object).BatchAsync(batch, CancellationToken.None);

            var result = answer.Should().BeOfType<ObjectResult>().Subject;
            result.StatusCode.Should().Be(refusal.Status);
            result.Value.Should().BeSameAs(refusal);
        }
    }
}
