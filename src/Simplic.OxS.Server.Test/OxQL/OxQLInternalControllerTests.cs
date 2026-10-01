using System.Text.Json;
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
    /// <summary>
    /// The internal batch and explain routes: admitted by the internal api key only, served by the
    /// shared query service as an internal call, which alone may carry the keyed fetch's <c>keyedBy</c>.
    /// </summary>
    public sealed class OxQLInternalControllerTests
    {
        private const string Key = "0e46ea43-6b5e-4b31-8008-95df146cf97d";

        private static OxQLInternalController Controller(IOxQLQueryService service, OxQLOptions? options = null) =>
            new(service, options ?? new OxQLOptions(), NullLogger<OxQLInternalController>.Instance)
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
            typeof(OxQLInternalController).GetMethod(nameof(OxQLInternalController.ExplainAsync))!
                .GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>().Single().Template.Should().Be("explain");
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
            service.Setup(s => s.BatchAsync(batch, true, It.IsAny<CancellationToken>())).ReturnsAsync(new BatchOutcome.Success(response));

            var answer = await Controller(service.Object).BatchAsync(batch, CancellationToken.None);

            answer.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
        }

        [Fact]
        public async Task Batch_AnswersARefusalWithItsStatus()
        {
            var batch = new BatchRequest { Queries = [] };
            var refusal = Refusal.Validation([new QueryValidationError { Code = "BATCH_TOO_LARGE", Message = "too many" }]);
            var service = new Mock<IOxQLQueryService>(MockBehavior.Strict);
            service.Setup(s => s.BatchAsync(batch, true, It.IsAny<CancellationToken>())).ReturnsAsync(new BatchOutcome.Refused(refusal));

            var answer = await Controller(service.Object).BatchAsync(batch, CancellationToken.None);

            var result = answer.Should().BeOfType<ObjectResult>().Subject;
            result.StatusCode.Should().Be(refusal.Status);
            result.Value.Should().BeSameAs(refusal);
        }

        [Fact]
        public async Task Batch_RunsAsAnInternalCall_SoAKeyedFetchReachesTheService()
        {
            var keyed = JsonSerializer.Deserialize<QueryRequest>(
                """{"entityType":"vehicle.vehicle","keyedBy":{"path":"id","keys":["0e46ea43-6b5e-4b31-8008-95df146cf97d"],"perKey":2},"pipeline":[]}""",
                OxQLJson.Wire)!;
            var batch = new BatchRequest { Queries = [keyed], MaxTimeMs = 250 };
            var response = new BatchResponse { Results = [] };

            // Strict: the public overload (internalCall false, or the one without the flag) is never called.
            var service = new Mock<IOxQLQueryService>(MockBehavior.Strict);
            service.Setup(s => s.BatchAsync(batch, true, It.IsAny<CancellationToken>())).ReturnsAsync(new BatchOutcome.Success(response));

            var answer = await Controller(service.Object).BatchAsync(batch, CancellationToken.None);

            answer.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
            keyed.KeyedBy.Should().NotBeNull();
            service.Verify(s => s.BatchAsync(batch, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Explain_AnswersTheQueryServicesAnswer_AsAnInternalCall()
        {
            var request = new ExplainRequest { Query = new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] }, Remote = ExplainRequest.RemoteCheck, IsEnvelope = true };
            var result = ExplainResult.Invalid(2, new ExplainEngine { Capabilities = [] }, [new QueryValidationError { Code = "UNKNOWN_PATH", Message = "no such path" }]);
            var service = new Mock<IOxQLQueryService>(MockBehavior.Strict);
            service.Setup(s => s.ExplainAsync(request, true, It.IsAny<CancellationToken>())).ReturnsAsync(new ExplainOutcome.Success(result));

            var answer = await Controller(service.Object).ExplainAsync(request, CancellationToken.None);

            answer.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(result);
        }

        [Fact]
        public async Task Explain_AnswersARefusalWithItsStatus()
        {
            var request = new ExplainRequest { Query = new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] } };
            var refusal = Refusal.Validation([new QueryValidationError { Code = "REQUEST_TOO_LARGE", Message = "too many catalog entries" }]);
            var service = new Mock<IOxQLQueryService>(MockBehavior.Strict);
            service.Setup(s => s.ExplainAsync(request, true, It.IsAny<CancellationToken>())).ReturnsAsync(new ExplainOutcome.Refused(refusal));

            var answer = await Controller(service.Object).ExplainAsync(request, CancellationToken.None);

            var objectResult = answer.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(refusal.Status);
            objectResult.Value.Should().BeSameAs(refusal);
        }

        [Fact]
        public async Task Explain_OfACallerWithItsPlacesInFlightTaken_Is429WithRetryAfter_AndAnotherCallerIsAdmitted()
        {
            var options = new OxQLOptions();
            options.Explain.MaxConcurrentPerCaller = 2;

            var request = new ExplainRequest { Query = new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] } };
            var result = ExplainResult.Invalid(2, new ExplainEngine { Capabilities = [] }, []);
            var release = new TaskCompletionSource<ExplainOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var service = new Mock<IOxQLQueryService>(MockBehavior.Strict);
            service.Setup(s => s.ExplainAsync(request, true, It.IsAny<CancellationToken>())).Returns(() =>
            {
                Interlocked.Increment(ref calls);

                return release.Task;
            });

            var first = From("logistics").ExplainAsync(request, CancellationToken.None);
            var second = From("logistics").ExplainAsync(request, CancellationToken.None);

            // The third of the same service is refused at once, before the query service is asked.
            var refusing = From("logistics");
            var refused = (await refusing.ExplainAsync(request, CancellationToken.None)).Should().BeOfType<ObjectResult>().Subject;

            refused.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            refusing.Response.Headers.RetryAfter.ToString().Should().Be("1");

            var error = refused.Value.Should().BeOfType<Refusal>().Subject.Errors!.Should().ContainSingle().Subject;
            error.Code.Should().Be("EXPLAIN_LIMIT");
            error.Params!["limit"].Should().Be("concurrentPerCaller");
            error.Params["max"].Should().Be(2);
            calls.Should().Be(2);

            // Another service has places of its own; so has every call that names none, together.
            var other = From("vehicle").ExplainAsync(request, CancellationToken.None);
            var unnamed = From(null).ExplainAsync(request, CancellationToken.None);

            calls.Should().Be(4);

            release.SetResult(new ExplainOutcome.Success(result));
            (await first).Should().BeOfType<OkObjectResult>();
            (await second).Should().BeOfType<OkObjectResult>();
            (await other).Should().BeOfType<OkObjectResult>();
            (await unnamed).Should().BeOfType<OkObjectResult>();

            // Answered: the places are free again.
            (await From("logistics").ExplainAsync(request, CancellationToken.None)).Should().BeOfType<OkObjectResult>();

            OxQLInternalController From(string? caller)
            {
                var controller = Controller(service.Object, options);

                if (caller is not null)
                    controller.Request.Headers[RemoteQueryClient.CallerHeader] = caller;

                return controller;
            }
        }

        [Fact]
        public async Task Explain_WhileExplainIsSwitchedOff_Is404WithoutCallingTheService()
        {
            var options = new OxQLOptions();
            options.Explain.Enabled = false;
            var service = new Mock<IOxQLQueryService>(MockBehavior.Strict);

            var answer = await Controller(service.Object, options).ExplainAsync(new ExplainRequest { Query = new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] } }, CancellationToken.None);

            answer.Should().BeOfType<NotFoundResult>();
            service.VerifyNoOtherCalls();
        }
    }
}
