using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Simplic.OxS.Server.Middleware;

namespace Simplic.OxS.Server.Test.Middleware;

/// <summary>
/// Guards the correlation-id contract: the id the caller is told about must be the id that ends
/// up in <see cref="IRequestContext"/> and therefore in the logs.
/// </summary>
public class CorrelationIdMiddlewareTest
{
    /// <summary>
    /// Minimal response feature that actually records <c>OnStarting</c> callbacks.
    /// <see cref="DefaultHttpContext"/>'s built-in feature discards them, so response headers set
    /// from a callback would otherwise be invisible to the test.
    /// </summary>
    private sealed class RecordingResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public string? ReasonPhrase { get; set; }
        public int StatusCode { get; set; } = 200;

        public void OnCompleted(Func<object, Task> callback, object state) { }

        public void OnStarting(Func<object, Task> callback, object state)
            => _onStarting.Add((callback, state));

        /// <summary>Simulates the server beginning to write the response.</summary>
        public async Task StartResponseAsync()
        {
            // Callbacks run in reverse registration order, matching Kestrel.
            for (var i = _onStarting.Count - 1; i >= 0; i--)
                await _onStarting[i].Callback(_onStarting[i].State);

            HasStarted = true;
        }
    }

    private static (HttpContext Context, RecordingResponseFeature Response) CreateContext()
    {
        // Build the feature collection before constructing the context, so the recording feature
        // is the one DefaultHttpContext binds to rather than relying on a post-construction swap.
        var response = new RecordingResponseFeature();

        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(response);

        return (new DefaultHttpContext(features), response);
    }

    private static CorrelationIdMiddleware CreateMiddleware(RequestDelegate next)
        => new(next, NullLogger<CorrelationIdMiddleware>.Instance);

    [Fact]
    public async Task Invoke_CallerSuppliedCorrelationId_IsPreservedOnRequestAndResponse()
    {
        // This is the regression that mattered: the caller's id used to be discarded, a fresh
        // Guid returned on the response, and the caller's original left on the request — so the
        // id the caller saw appeared in no log entry.
        var supplied = Guid.NewGuid();
        var (context, response) = CreateContext();
        context.Request.Headers[Constants.HttpHeaderCorrelationIdKey] = supplied.ToString();

        Guid? observedDuringRequest = null;

        var middleware = CreateMiddleware(ctx =>
        {
            observedDuringRequest = Guid.Parse(ctx.Request.Headers[Constants.HttpHeaderCorrelationIdKey]!);
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);
        await response.StartResponseAsync();

        observedDuringRequest.Should().Be(supplied, "the caller's correlation id must not be replaced");
        response.Headers[Constants.HttpHeaderCorrelationIdKey].ToString().Should().Be(supplied.ToString());
    }

    [Fact]
    public async Task Invoke_NoCorrelationId_GeneratesOneAndWritesItToRequestAndResponse()
    {
        var (context, response) = CreateContext();

        Guid? observedDuringRequest = null;

        var middleware = CreateMiddleware(ctx =>
        {
            observedDuringRequest = Guid.Parse(ctx.Request.Headers[Constants.HttpHeaderCorrelationIdKey]!);
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);
        await response.StartResponseAsync();

        observedDuringRequest.Should().NotBeNull().And.NotBe(Guid.Empty);
        response.Headers[Constants.HttpHeaderCorrelationIdKey].ToString()
            .Should().Be(observedDuringRequest!.Value.ToString(),
                "the response must report the same id that was visible to the request");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Invoke_UnusableCorrelationId_IsReplacedWithAGeneratedGuid(string supplied)
    {
        // IRequestContext.CorrelationId is a Guid and InternalClient forwards it downstream, so a
        // value that cannot round-trip as a Guid has to be replaced rather than propagated.
        var (context, response) = CreateContext();
        context.Request.Headers[Constants.HttpHeaderCorrelationIdKey] = supplied;

        Guid? observedDuringRequest = null;

        var middleware = CreateMiddleware(ctx =>
        {
            observedDuringRequest = Guid.Parse(ctx.Request.Headers[Constants.HttpHeaderCorrelationIdKey]!);
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);
        await response.StartResponseAsync();

        observedDuringRequest.Should().NotBeNull().And.NotBe(Guid.Empty);
        observedDuringRequest!.Value.ToString().Should().NotBe(supplied);
        response.Headers[Constants.HttpHeaderCorrelationIdKey].ToString()
            .Should().Be(observedDuringRequest.Value.ToString());
    }

    [Fact]
    public async Task Invoke_RequestAndResponseIds_AlwaysMatch()
    {
        // The property that makes support tickets actionable, asserted directly.
        var (context, response) = CreateContext();
        context.Request.Headers[Constants.HttpHeaderCorrelationIdKey] = Guid.NewGuid().ToString();

        string? requestId = null;

        var middleware = CreateMiddleware(ctx =>
        {
            requestId = ctx.Request.Headers[Constants.HttpHeaderCorrelationIdKey].ToString();
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);
        await response.StartResponseAsync();

        response.Headers[Constants.HttpHeaderCorrelationIdKey].ToString().Should().Be(requestId);
    }
}
