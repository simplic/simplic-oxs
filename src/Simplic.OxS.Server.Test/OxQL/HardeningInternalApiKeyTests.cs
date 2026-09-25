using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Test.OxSchema;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>
    /// The guard of every internal controller, exercised through a running host: only the
    /// configured key passes, a host whose key is blank is closed, and a refusal never runs
    /// the action.
    /// </summary>
    public sealed class HardeningInternalApiKeyTests
    {
        private const string Key = "c0ffee00-1111-2222-3333-444455556666";
        private const string Unauthorized = "Internal-api-key (i-api-key) is not valid or not provided";

        private static Task<IHost> StartAsync(string? configuredKey, ProbeCalls? calls = null) =>
            HardeningTestHost.StartAsync([typeof(HardeningInternalProbeController), typeof(HardeningPlainProbeController)], services =>
            {
                services.AddSingleton(calls ?? new ProbeCalls());
                services.Configure<AuthSettings>(settings => settings.InternalApiKey = configuredKey!);
            });

        /// <summary>Sends the header exactly as given; an <see cref="HttpClient"/> would normalise it first.</summary>
        private static async Task<(int Status, string Body)> GetAsync(IHost host, string path, string? authorization)
        {
            var context = await host.GetTestServer().SendAsync(request =>
            {
                request.Request.Method = HttpMethods.Get;
                request.Request.Path = path;

                if (authorization is not null)
                    request.Request.Headers.Authorization = authorization;
            });

            using var reader = new StreamReader(context.Response.Body);

            return (context.Response.StatusCode, await reader.ReadToEndAsync());
        }

        [Fact]
        public async Task TheCorrectKey_IsAdmitted()
        {
            using var host = await StartAsync(Key);

            var (status, body) = await GetAsync(host, "/internal/hardening-probe", $"i-api-key {Key}");

            status.Should().Be((int)HttpStatusCode.OK);
            body.Should().Be("reached");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("i-api-key")]
        [InlineData("Bearer " + Key)]
        [InlineData("I-API-KEY " + Key)]
        [InlineData("i-api-key wrong")]
        [InlineData("i-api-key " + Key + "0")]
        [InlineData("i-api-key c0ffee00-1111-2222-3333-44445555666")]
        [InlineData("i-api-key " + Key + " trailing")]
        public async Task EverythingButTheCorrectKey_IsRefusedWithTheSameAnswer(string? authorization)
        {
            var calls = new ProbeCalls();
            using var host = await StartAsync(Key, calls);

            var (status, body) = await GetAsync(host, "/internal/hardening-probe", authorization);

            status.Should().Be((int)HttpStatusCode.Unauthorized);
            body.Should().Be(Unauthorized);
            calls.Count.Should().Be(0);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task ABlankConfiguredKey_DoesNotAdmitABlankPresentedKey(string? configuredKey)
        {
            var calls = new ProbeCalls();
            using var host = await StartAsync(configuredKey, calls);

            var (status, body) = await GetAsync(host, "/internal/hardening-probe", "i-api-key ");

            status.Should().Be((int)HttpStatusCode.Unauthorized);
            body.Should().Be(Unauthorized);
            calls.Count.Should().Be(0);
        }

        [Theory]
        [InlineData(null, "i-api-key anything")]
        [InlineData("", "i-api-key anything")]
        [InlineData(" ", "i-api-key anything")]
        [InlineData(" ", "i-api-key  ")]
        [InlineData("\t", "i-api-key \t")]
        public async Task ABlankConfiguredKey_AdmitsNoHeaderAtAll(string? configuredKey, string authorization)
        {
            var calls = new ProbeCalls();
            using var host = await StartAsync(configuredKey, calls);

            var (status, _) = await GetAsync(host, "/internal/hardening-probe", authorization);

            status.Should().Be((int)HttpStatusCode.Unauthorized);
            calls.Count.Should().Be(0);
        }

        [Fact]
        public async Task TheCorrectKey_OnAControllerThatIsNotInternal_IsRefusedWithoutRunningTheAction()
        {
            var calls = new ProbeCalls();
            using var host = await StartAsync(Key, calls);

            var (status, body) = await GetAsync(host, "/hardening-plain-probe", $"i-api-key {Key}");

            status.Should().Be((int)HttpStatusCode.BadRequest);
            body.Should().StartWith("Internal calls are only allowed for OxSInternalController.");
            calls.Count.Should().Be(0);
        }
    }

    /// <summary>How often a probe action ran.</summary>
    public sealed class ProbeCalls
    {
        private int count;

        /// <summary>The number of executed probe actions.</summary>
        public int Count => count;

        /// <summary>Records one executed action.</summary>
        public void Record() => Interlocked.Increment(ref count);
    }

    /// <summary>An internal controller as a service writes one.</summary>
    [ApiController]
    [Route("internal/hardening-probe")]
    public sealed class HardeningInternalProbeController(ProbeCalls calls) : OxSInternalController
    {
        /// <summary>Answers once the guard admitted the call.</summary>
        [HttpGet]
        public IActionResult Get(CancellationToken ct)
        {
            calls.Record();

            return Content("reached");
        }
    }

    /// <summary>A controller that carries the guard without deriving from the internal base class.</summary>
    [ApiController]
    [AuthorizeInternalApiKey]
    [Route("hardening-plain-probe")]
    public sealed class HardeningPlainProbeController(ProbeCalls calls) : ControllerBase
    {
        /// <summary>Must never run: the guard refuses this controller.</summary>
        [HttpGet]
        public IActionResult Get(CancellationToken ct)
        {
            calls.Record();

            return Content("reached");
        }
    }
}
