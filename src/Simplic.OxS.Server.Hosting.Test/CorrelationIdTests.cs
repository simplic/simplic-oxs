using Microsoft.AspNetCore.TestHost;
using Simplic.OxS.Server.Hosting.Test.Fixtures;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>The correlation id an answer of a host carries in <c>X-Correlation-ID</c>.</summary>
    public sealed class CorrelationIdTests
    {
        private const string Header = "X-Correlation-ID";

        private const string Route = "/parcel-api/v1/Parcel/open";

        private static async Task<string[]> AnswerAsync(params string[] sent)
        {
            // The route is anonymous; what authenticates the host's other routes needs a cache all the same.
            using var host = await BootstrapHost.StartAsync<ParcelStartup>(new AnsweringEngine().Register);
            using var client = host.GetTestClient();
            using var message = new HttpRequestMessage(HttpMethod.Get, Route);

            foreach (var value in sent)
                message.Headers.TryAddWithoutValidation(Header, value);

            using var answer = await client.SendAsync(message);

            ((int)answer.StatusCode).Should().Be(200, await answer.Content.ReadAsStringAsync());

            return [.. answer.Headers.GetValues(Header)];
        }

        private static bool IsAnId(string value) => Guid.TryParse(value, out _);

        [Theory]
        [InlineData("6f1c2a54-0b7e-4d39-a1c8-93e5d2f4b607")]
        [InlineData("6F1C2A54-0B7E-4D39-A1C8-93E5D2F4B607")]
        [InlineData("6f1c2a540b7e4d39a1c893e5d2f4b607")]
        public async Task TheAnswer_CarriesTheIdTheCallerSent_AsTheCallerWroteIt(string sent)
        {
            (await AnswerAsync(sent)).Should().Equal(sent);
        }

        [Fact]
        public async Task TheAnswer_CarriesAnIdOfItsOwn_WhereTheCallerSentNone()
        {
            var first = await AnswerAsync();
            var second = await AnswerAsync();

            IsAnId(first.Should().ContainSingle().Which).Should().BeTrue();
            second.Should().ContainSingle().Which.Should().NotBe(first[0]);
        }

        [Theory]
        [InlineData("not-an-id")]
        [InlineData("")]
        [InlineData("X-Correlation-ID")]
        public async Task TheAnswer_CarriesAnIdOfItsOwn_WhereWhatTheCallerSentIsNoId(string sent)
        {
            var answered = await AnswerAsync(sent);

            answered.Should().ContainSingle().Which.Should().NotBe(sent);
            IsAnId(answered[0]).Should().BeTrue();
        }

        [Fact]
        public async Task TheAnswer_CarriesTheFirstId_WhereTheCallerSentSeveral()
        {
            const string First = "6f1c2a54-0b7e-4d39-a1c8-93e5d2f4b607";

            (await AnswerAsync(First, "0d9b7c1e-52a4-4f6b-8e30-a7c1d5e9f204")).Should().Equal(First);
        }
    }
}
