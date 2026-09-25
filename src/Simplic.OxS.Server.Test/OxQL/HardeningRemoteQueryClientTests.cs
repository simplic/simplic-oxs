using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxQL.Core.Models;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>The remote batch call is always bounded in time, and a host entry that forms no address is an unreachable owner.</summary>
    public sealed class HardeningRemoteQueryClientTests
    {
        /// <summary>An owner that never answers: the call ends only when its token is cancelled.</summary>
        private sealed class SilentHandler : HttpMessageHandler
        {
            public int Calls { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;

                await Task.Delay(Timeout.Infinite, cancellationToken);

                throw new InvalidOperationException("The wait above only ends by cancellation.");
            }
        }

        private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        }

        private static RemoteQueryClient Client(HttpMessageHandler handler, string host) =>
            new(
                new Factory(handler),
                Options.Create(new AuthSettings { InternalApiKey = "key" }),
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["InternalHosts:vehicle"] = host }).Build(),
                new HttpContextAccessor(),
                NullLogger<RemoteQueryClient>.Instance);

        private static BatchRequest Batch() => new() { Queries = [new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] }] };

        [Theory(Timeout = 10_000)]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Batch_WithoutAPositiveBudget_IsStillBounded(int budgetMs)
        {
            var handler = new SilentHandler();
            var client = Client(handler, "localhost:8080");
            client.FallbackBudget = TimeSpan.FromMilliseconds(50);

            var call = () => client.BatchAsync("vehicle", Batch(), TimeSpan.FromMilliseconds(budgetMs), CancellationToken.None);

            await call.Should().ThrowAsync<OperationCanceledException>();
            handler.Calls.Should().Be(1);
        }

        [Fact]
        public void TheFallbackBudget_IsTheQueryEnginesDefaultCeiling()
        {
            Client(new SilentHandler(), "localhost:8080").FallbackBudget.Should().Be(TimeSpan.FromMilliseconds(new ExecutionOptions().MaxTimeMs));
        }

        [Theory]
        [InlineData("vehicle:not-a-port")]
        [InlineData("[::1")]
        public async Task Batch_ToAHostEntryThatFormsNoAddress_FailsAsAnUnreachableOwner(string host)
        {
            var handler = new SilentHandler();
            var client = Client(handler, host);

            var call = () => client.BatchAsync("vehicle", Batch(), TimeSpan.FromSeconds(1), CancellationToken.None);

            await call.Should().ThrowAsync<HttpRequestException>();
            handler.Calls.Should().Be(0);
        }

        [Theory]
        [InlineData("vehicle:not-a-port")]
        [InlineData("[::1")]
        public async Task IsReachable_ForAHostEntryThatFormsNoAddress_IsFalse(string host)
        {
            var handler = new SilentHandler();

            (await Client(handler, host).IsReachableAsync("vehicle", CancellationToken.None)).Should().BeFalse();
            handler.Calls.Should().Be(0);
        }
    }
}
