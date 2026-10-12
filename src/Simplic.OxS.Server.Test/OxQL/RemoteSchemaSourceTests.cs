using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxQL.Core.Engine;
using OxQL.Model.Attributes;
using OxQL.Model.Build;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>
    /// The remote client as the source of the owners' schema documents: one conditional read of
    /// the owner's <c>/schema</c> over the owner's route, kept so an owner is asked rarely, and
    /// never a failure of whoever asked: an owner that cannot be read is not known, not wrong.
    /// </summary>
    public sealed class RemoteSchemaSourceTests
    {
        private const string Revision = "sha256:9c1b0e7d5a3f4c2b8e6d4f2a1c3b5d7e9f1a2b3c4d5e6f708192a3b4c5d6e7f8";
        private const string ETag = "\"9c1b0e7d5a3f4c2b8e6d4f2a1c3b5d7e9f1a2b3c4d5e6f708192a3b4c5d6e7f8\"";

        /// <summary>The document of a depot service: two entities, one with a retired id and a legacy model id, and a structural type.</summary>
        private const string Document = """
            {
              "schemaVersion": "1.1", "service": "depot", "api": { "name": "depot-api", "version": "v2" },
              "revision": "sha256:9c1b0e7d5a3f4c2b8e6d4f2a1c3b5d7e9f1a2b3c4d5e6f708192a3b4c5d6e7f8",
              "types": {
                "depot.bay": { "entity": true, "aliases": ["bay", "$BayModel"], "properties": [] },
                "depot.gate": { "entity": true, "aliases": [], "properties": [] },
                "t_slot": { "properties": [] }
              }
            }
            """;

        /// <summary>An owner whose answer the test decides per call, and that keeps what it was sent.</summary>
        private sealed class OwnerHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
        {
            public List<(Uri Uri, IReadOnlyDictionary<string, string> Headers)> Sent { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Sent.Add((request.RequestUri!, request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase)));

                return answer(request, cancellationToken);
            }
        }

        private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        }

        private sealed class ManualTime(DateTimeOffset start) : TimeProvider
        {
            private DateTimeOffset now = start;

            public override DateTimeOffset GetUtcNow() => now;

            public void Advance(TimeSpan by) => now += by;
        }

        private static HttpResponseMessage Served(string body = Document, string? etag = ETag)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

            if (etag is not null)
                response.Headers.ETag = EntityTagHeaderValue.Parse(etag);

            return response;
        }

        /// <summary>An owner that serves the document, and 304 to a request that names its entity tag.</summary>
        private static OwnerHandler Owner() => new((request, _) =>
            Task.FromResult(request.Headers.TryGetValues("If-None-Match", out var named) && named.Contains(ETag)
                ? new HttpResponseMessage(HttpStatusCode.NotModified)
                : Served()));

        private static (RemoteQueryClient Client, ManualTime Time) Client(HttpMessageHandler handler)
        {
            var time = new ManualTime(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var client = new RemoteQueryClient(
                new Factory(handler),
                Options.Create(new AuthSettings { InternalApiKey = "the-internal-key" }),
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["InternalHosts:depot"] = "depot-host:8080",
                    ["InternalApiVersions:depot"] = "v2",
                    ["InternalHosts:yard"] = "not a host",
                }).Build(),
                new HttpContextAccessor(),
                NullLogger<RemoteQueryClient>.Instance)
            {
                Time = time,
                SchemaTtl = TimeSpan.FromSeconds(10),
            };

            return (client, time);
        }

        [Fact]
        public async Task TheOwnersDocument_GivesItsRevisionAndEveryIdItAnswersAQueryUnder()
        {
            var owner = Owner();
            var (client, _) = Client(owner);

            var read = await client.SchemaEntitiesAsync("depot", knownRevision: null, CancellationToken.None);

            read!.Revision.Should().Be(Revision);
            read.IsNotModified.Should().BeFalse();
            read.EntityIds.Should().BeEquivalentTo(["depot.bay", "bay", "depot.gate"], "the entities and the ids they retired; a legacy model id and a structural type name no entity of a query");

            // The owner's route, as every other owner call takes it; the document is anonymous, so no key and no identity travel.
            var sent = owner.Sent.Should().ContainSingle().Subject;

            sent.Uri.Should().Be(new Uri("http://depot-host:8080/depot-api/v2/schema"));
            sent.Headers.Should().NotContainKey("Authorization").And.NotContainKey("If-None-Match").And.NotContainKey("OrganizationId").And.NotContainKey("UserId");
        }

        [Fact]
        public async Task WhatWasRead_AnswersWithoutAskingTheOwnerAgainWithinItsTimeToLive()
        {
            var owner = Owner();
            var (client, time) = Client(owner);

            await client.SchemaEntitiesAsync("depot", null, CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(9));

            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None))!.EntityIds.Should().HaveCount(3);
            (await client.SchemaEntitiesAsync("depot", Revision, CancellationToken.None))!.IsNotModified.Should().BeTrue("the caller holds that revision");
            owner.Sent.Should().ContainSingle();
        }

        [Fact]
        public async Task AfterItsTimeToLive_TheReadIsConditional_AndAnUnchangedDocumentIsNotSentAgain()
        {
            var owner = Owner();
            var (client, time) = Client(owner);

            await client.SchemaEntitiesAsync("depot", null, CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(11));

            var again = await client.SchemaEntitiesAsync("depot", Revision, CancellationToken.None);

            again.Should().Be(RemoteSchemaEntities.NotModified(Revision));
            owner.Sent.Should().HaveCount(2);
            owner.Sent[1].Headers["If-None-Match"].Should().Be(ETag);

            // A caller that holds nothing gets the entities that were kept, from the same 304.
            time.Advance(TimeSpan.FromSeconds(11));

            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None))!.EntityIds.Should().BeEquivalentTo(["depot.bay", "bay", "depot.gate"]);
            owner.Sent.Should().HaveCount(3);
            owner.Sent[2].Headers["If-None-Match"].Should().Be(ETag);
        }

        [Fact]
        public async Task ARevisionTheCallerHolds_IsTheValidatorOfAClientThatHoldsNothing()
        {
            var owner = Owner();
            var (client, _) = Client(owner);

            var read = await client.SchemaEntitiesAsync("depot", Revision, CancellationToken.None);

            read.Should().Be(RemoteSchemaEntities.NotModified(Revision));
            owner.Sent.Should().ContainSingle().Which.Headers["If-None-Match"].Should().Be(ETag);
        }

        [Fact]
        public async Task ADocumentThatChanged_IsReadAgain()
        {
            const string changed = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
            var served = 0;
            var owner = new OwnerHandler((_, _) => Task.FromResult(served++ == 0
                ? Served()
                : Served(Document.Replace(Revision, changed).Replace("\"depot.gate\"", "\"depot.dock\""), "\"1111111111111111111111111111111111111111111111111111111111111111\"")));
            var (client, time) = Client(owner);

            await client.SchemaEntitiesAsync("depot", null, CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(11));

            var read = await client.SchemaEntitiesAsync("depot", Revision, CancellationToken.None);

            read!.Revision.Should().Be(changed);
            read.EntityIds.Should().BeEquivalentTo(["depot.bay", "bay", "depot.dock"]);
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.NotModified)]
        public async Task AnOwnerThatAnswersNoDocument_IsNotKnown_AndIsNotAskedAgainWithinTheTimeToLive(HttpStatusCode status)
        {
            var owner = new OwnerHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
            var (client, time) = Client(owner);

            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None)).Should().BeNull();
            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None)).Should().BeNull();
            owner.Sent.Should().ContainSingle();

            time.Advance(TimeSpan.FromSeconds(11));
            await client.SchemaEntitiesAsync("depot", null, CancellationToken.None);
            owner.Sent.Should().HaveCount(2);
        }

        [Fact]
        public async Task AnOwnerThatDoesNotAnswerInTime_IsNotKnown_AndNothingIsThrown()
        {
            var owner = new OwnerHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                throw new InvalidOperationException("The wait above only ends by cancellation.");
            });
            var (client, _) = Client(owner);

            client.SchemaBudget = TimeSpan.FromMilliseconds(50);

            var read = await client.SchemaEntitiesAsync("depot", null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            read.Should().BeNull();
        }

        [Fact]
        public async Task AnOwnerThatCannotBeReached_IsNotKnown_AndNothingIsThrown()
        {
            var owner = new OwnerHandler((_, _) => throw new HttpRequestException("No connection could be made."));
            var (client, _) = Client(owner);

            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None)).Should().BeNull();
        }

        [Fact]
        public async Task ACallerThatGivesUp_GetsNothingAndNothingIsThrown_AndTheOwnerIsAskedAgainByTheNext()
        {
            using var given = new CancellationTokenSource();
            var owner = new OwnerHandler(async (_, token) =>
            {
                given.Cancel();
                await Task.Delay(Timeout.Infinite, token);

                throw new InvalidOperationException("The wait above only ends by cancellation.");
            });
            var (client, _) = Client(owner);

            (await client.SchemaEntitiesAsync("depot", null, given.Token)).Should().BeNull();

            // Nothing was learned of the owner, so nothing is kept.
            await client.SchemaEntitiesAsync("depot", null, new CancellationToken(canceled: true));
            owner.Sent.Should().HaveCount(2);
        }

        /// <summary>An owner whose every answer waits until the test gives it.</summary>
        private static (OwnerHandler Owner, TaskCompletionSource<HttpResponseMessage> Answer) HeldOwner()
        {
            var answer = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            return (new OwnerHandler((_, token) => answer.Task.WaitAsync(token)), answer);
        }

        [Fact]
        public async Task CallersThatAskWhileAReadIsUnderWay_ShareIt()
        {
            var answer = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var held = false;
            var owner = new OwnerHandler((_, token) => held ? answer.Task.WaitAsync(token) : Task.FromResult(Served()));
            var (client, time) = Client(owner);

            await client.SchemaEntitiesAsync("depot", null, CancellationToken.None);

            held = true;
            time.Advance(TimeSpan.FromSeconds(11));

            var callers = Enumerable.Range(0, 8).Select(caller => client.SchemaEntitiesAsync("depot", caller % 2 == 0 ? null : Revision, CancellationToken.None)).ToArray();

            callers.Should().OnlyContain(caller => !caller.IsCompleted);
            answer.SetResult(new HttpResponseMessage(HttpStatusCode.NotModified));

            var read = await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(30));

            // One conditional read for the eight, and each is told what it asked: the entities, or that its revision is current.
            owner.Sent.Should().HaveCount(2);
            owner.Sent[1].Headers["If-None-Match"].Should().Be(ETag);
            read.Where((_, caller) => caller % 2 == 0).Select(entities => entities?.EntityIds?.Count).Should().OnlyContain(count => count == 3);
            read.Where((_, caller) => caller % 2 == 1).Should().OnlyContain(entities => entities == RemoteSchemaEntities.NotModified(Revision));

            // The read is over: the next one after the time to live asks the owner again.
            held = false;
            time.Advance(TimeSpan.FromSeconds(11));
            await client.SchemaEntitiesAsync("depot", null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            owner.Sent.Should().HaveCount(3);
        }

        [Fact]
        public async Task CallersOfAClientThatHoldsNothing_ShareTheReadOfTheRevisionTheyHold_AndNoOther()
        {
            const string other = "sha256:1111111111111111111111111111111111111111111111111111111111111111";

            var (owner, answer) = HeldOwner();
            var (client, _) = Client(owner);

            var holders = Enumerable.Range(0, 3).Select(_ => client.SchemaEntitiesAsync("depot", Revision, CancellationToken.None)).ToArray();
            var stranger = client.SchemaEntitiesAsync("depot", other, CancellationToken.None);

            // The validator of such a read is the caller's own revision, so a caller that holds another one is not answered by it.
            owner.Sent.Should().HaveCount(2);
            owner.Sent.Select(sent => sent.Headers["If-None-Match"]).Should().Equal(ETag, "\"1111111111111111111111111111111111111111111111111111111111111111\"");

            answer.SetResult(new HttpResponseMessage(HttpStatusCode.NotModified));

            (await Task.WhenAll(holders).WaitAsync(TimeSpan.FromSeconds(30))).Should().OnlyContain(entities => entities == RemoteSchemaEntities.NotModified(Revision));
            (await stranger.WaitAsync(TimeSpan.FromSeconds(30))).Should().Be(RemoteSchemaEntities.NotModified(other));
        }

        [Fact]
        public async Task ACallerThatGivesUp_DoesNotEndTheReadForTheOthers()
        {
            using var given = new CancellationTokenSource();
            var (owner, answer) = HeldOwner();
            var (client, _) = Client(owner);

            var impatient = client.SchemaEntitiesAsync("depot", null, given.Token);
            var patient = client.SchemaEntitiesAsync("depot", null, CancellationToken.None);

            given.Cancel();

            (await impatient.WaitAsync(TimeSpan.FromSeconds(30))).Should().BeNull();
            patient.IsCompleted.Should().BeFalse("the read goes on for the caller that waits");

            answer.SetResult(Served());

            (await patient.WaitAsync(TimeSpan.FromSeconds(30)))!.EntityIds.Should().HaveCount(3);
            owner.Sent.Should().ContainSingle();
        }

        [Fact]
        public async Task ADocumentLargerThanTheClientReads_IsNotKnown_AndIsNotAskedForAgainWithinTheTimeToLive()
        {
            var owner = new OwnerHandler((_, _) => Task.FromResult(Served()));
            var (client, time) = Client(owner);

            client.SchemaBodyLimit = Encoding.UTF8.GetByteCount(Document) - 1;

            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None)).Should().BeNull();
            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None)).Should().BeNull();
            owner.Sent.Should().ContainSingle();

            // A document of exactly the bound is read.
            client.SchemaBodyLimit = Encoding.UTF8.GetByteCount(Document);
            time.Advance(TimeSpan.FromSeconds(11));

            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None))!.EntityIds.Should().HaveCount(3);
        }

        /// <summary>A body without a length, which counts how much of it was read.</summary>
        private sealed class EndlessBody : HttpContent
        {
            public long Read { get; private set; }

            protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new Endless(this));

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException("The body is read as a stream.");

            protected override bool TryComputeLength(out long length)
            {
                length = 0;

                return false;
            }

            private sealed class Endless(EndlessBody owner) : MemoryStream
            {
                public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                {
                    buffer.Span.Fill((byte)' ');
                    owner.Read += buffer.Length;

                    return ValueTask.FromResult(buffer.Length);
                }
            }
        }

        [Fact]
        public async Task ABodyWithoutALength_IsReadNoFurtherThanTheBound()
        {
            var body = new EndlessBody();
            var owner = new OwnerHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body }));
            var (client, _) = Client(owner);

            client.SchemaBodyLimit = 200_000;

            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30))).Should().BeNull();
            body.Read.Should().BeInRange(200_000, 400_000, "the read stops with the buffer that crosses the bound");
        }

        [Theory]
        [InlineData("this is not json")]
        [InlineData("[]")]
        [InlineData("""{ "revision": "sha256:00", "types": [] }""")]
        [InlineData("""{ "types": {} }""")]
        [InlineData("")]
        public async Task ABodyThatIsNoSchemaDocument_IsNotKnown(string body)
        {
            var owner = new OwnerHandler((_, _) => Task.FromResult(Served(body, etag: null)));
            var (client, _) = Client(owner);

            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None)).Should().BeNull();
        }

        [Fact]
        public async Task WhatWasReadBeforeAnOwnerStoppedAnswering_IsTheValidatorWhenItAnswersAgain()
        {
            var down = false;
            var owner = new OwnerHandler((request, token) => down
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
                : Task.FromResult(request.Headers.Contains("If-None-Match") ? new HttpResponseMessage(HttpStatusCode.NotModified) : Served()));
            var (client, time) = Client(owner);

            await client.SchemaEntitiesAsync("depot", null, CancellationToken.None);

            down = true;
            time.Advance(TimeSpan.FromSeconds(11));
            (await client.SchemaEntitiesAsync("depot", Revision, CancellationToken.None)).Should().BeNull("an owner that is down is not known, whatever was read of it before");
            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None)).Should().BeNull("and what was read before answers nobody meanwhile");

            down = false;
            time.Advance(TimeSpan.FromSeconds(11));

            (await client.SchemaEntitiesAsync("depot", null, CancellationToken.None))!.EntityIds.Should().HaveCount(3);
            owner.Sent.Should().HaveCount(3);
            owner.Sent[2].Headers["If-None-Match"].Should().Be(ETag);
        }

        [Fact]
        public async Task AServiceTheHostDoesNotKnow_OrWhoseEntryFormsNoAddress_IsNotKnownWithoutACall()
        {
            var owner = Owner();
            var (client, _) = Client(owner);

            (await client.SchemaEntitiesAsync("harbour", null, CancellationToken.None)).Should().BeNull();
            (await client.SchemaEntitiesAsync("yard", null, CancellationToken.None)).Should().BeNull();
            client.SchemaUrl("harbour").Should().BeNull();
            owner.Sent.Should().BeEmpty();
        }

        /// <summary>An entity of this host with references into the depot service.</summary>
        private sealed class Delivery
        {
            public Guid Id { get; set; }

            public Guid OrganizationId { get; set; }

            [OxQLReference("depot.bay", "id")]
            public Guid BayId { get; set; }

            [OxQLReference("depot.ramp", "id")]
            public Guid RampId { get; set; }
        }

        [Fact]
        public async Task TheQueryEnginesValidator_FindsTheClient_AndNamesTheTargetTheOwnerDoesNotPublish()
        {
            var owner = Owner();
            var (client, _) = Client(owner);
            var model = ClrModelBuilder.Build([new EntityDeclaration("route.delivery", "route.delivery", typeof(Delivery), "deliveries", null, false)]);
            var validator = new RemoteReferenceValidator();

            validator.State(model, client).Should().ContainSingle().Which.Validated.Should().BeFalse("the first check runs in the background");
            await validator.Refreshing.WaitAsync(TimeSpan.FromSeconds(30));

            var depot = validator.Of("depot")!;

            depot.Validated.Should().BeTrue();
            depot.Revision.Should().Be(Revision);
            depot.Declared.Should().Be(2);
            depot.UnknownTargets.Should().Equal("depot.ramp");
        }

        [Fact]
        public async Task TheQueryEnginesValidator_LeavesTheReferencesOfAnOwnerThatDoesNotAnswerNotValidated()
        {
            var owner = new OwnerHandler((_, _) => throw new HttpRequestException("No connection could be made."));
            var (client, _) = Client(owner);
            var model = ClrModelBuilder.Build([new EntityDeclaration("route.delivery", "route.delivery", typeof(Delivery), "deliveries", null, false)]);
            var validator = new RemoteReferenceValidator();

            validator.State(model, client);
            await validator.Refreshing.WaitAsync(TimeSpan.FromSeconds(30));

            var depot = validator.Of("depot")!;

            depot.Validated.Should().BeFalse();
            depot.Reason.Should().Be(RemoteReferenceValidation.Unavailable);
            depot.UnknownTargets.Should().BeEmpty("an owner that cannot be read makes no target wrong");
        }
    }
}
