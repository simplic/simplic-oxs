using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OxQL.Core.Models;
using OxQL.Model.Addon;
using Simplic.OxS.Server.Controllers;
using Simplic.OxS.Server.Controllers.Model;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.Services;
using Simplic.OxS.Server.Test.OxSchema;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>
    /// What a request waits for to have an organisation's addon definitions: nothing once the
    /// cache holds a value (one past its lifetime is served and read again in the background),
    /// one read for all the calls that miss together, and the repository again after a write.
    /// </summary>
    [Collection(SchemaCollection.Name)]
    public sealed class AddonDefinitionCacheTests
    {
        private const string Widget = "probe.widget";

        private static readonly Guid Organisation = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

        /// <summary>A clock a test moves.</summary>
        private sealed class Clock : TimeProvider
        {
            private long now = 1_000_000;

            public override long TimestampFrequency => 1_000;

            public override long GetTimestamp() => Interlocked.Read(ref now);

            public void Advance(TimeSpan by) => Interlocked.Add(ref now, (long)by.TotalMilliseconds);
        }

        /// <summary>A repository whose every read waits until the test lets it answer, and is counted.</summary>
        private sealed class Store
        {
            private readonly object gate = new();
            private readonly List<TaskCompletionSource<IEnumerable<AddonDefinitionDocument>>> pending = [];
            private TaskCompletionSource asked = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Store()
            {
                Repository.Setup(repository => repository.GetByEntitiesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                    .Returns((IReadOnlyCollection<string> _, Guid? _, CancellationToken token) =>
                    {
                        var read = new TaskCompletionSource<IEnumerable<AddonDefinitionDocument>>(TaskCreationOptions.RunContinuationsAsynchronously);

                        lock (gate)
                        {
                            Reads++;
                            Tokens.Add(token);
                            pending.Add(read);
                            asked.TrySetResult();
                        }

                        return read.Task;
                    });
            }

            public Mock<IAddonDefinitionRepository> Repository { get; } = new(MockBehavior.Strict);

            public int Reads { get; private set; }

            public List<CancellationToken> Tokens { get; } = [];

            /// <summary>Completes when a read is waiting for its answer.</summary>
            public async Task AskedAsync()
            {
                Task waiting;

                lock (gate)
                    waiting = asked.Task;

                await waiting.WaitAsync(Patience);
            }

            /// <summary>Answers every read that waits with the rows of <paramref name="paths"/>.</summary>
            public void Answer(params string[] paths) => Complete(read => read.SetResult(paths.Select(path => Document(path)).ToList()));

            /// <summary>Fails every read that waits.</summary>
            public void Fail(Exception exception) => Complete(read => read.SetException(exception));

            private void Complete(Action<TaskCompletionSource<IEnumerable<AddonDefinitionDocument>>> with)
            {
                List<TaskCompletionSource<IEnumerable<AddonDefinitionDocument>>> waiting;

                lock (gate)
                {
                    waiting = [.. pending];
                    pending.Clear();
                    asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                waiting.ForEach(with);
            }

            /// <summary>What a request's own read is: this repository, the model's union, the request's token.</summary>
            public Func<CancellationToken, Task<IReadOnlyList<AddonDefinition>>> Read =>
                token => AddonDefinitionSource.LoadAsync(Repository.Object, SchemaBuild.Degraded.Model, Widget, Organisation, token);

            /// <summary>Where the background read finds a repository of its own.</summary>
            public IServiceScopeFactory Scopes() => new ServiceCollection()
                .AddSingleton(SchemaBuild.Degraded)
                .AddScoped(_ => Repository.Object)
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
        }

        private static AddonDefinitionDocument Document(string path) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = Organisation,
            Entity = Widget,
            Path = path,
            Kind = "decimal",
        };

        private static IReadOnlyList<AddonDefinition> Held(params string[] paths) =>
            [.. paths.Select(path => new AddonDefinition { Id = Guid.NewGuid(), Entity = Widget, Path = path, Kind = AddonKind.Decimal })];

        private static OxQLOptions Options(int lifetimeSeconds = 30) => new() { Cache = { AddonDefinitionTtlSeconds = lifetimeSeconds } };

        private static Func<CancellationToken, Task<IReadOnlyList<AddonDefinition>>> NoRead =>
            _ => throw new InvalidOperationException("The request must not read the repository.");

        [Fact]
        public async Task AValuePastItsLifetime_IsServedAtOnce_AndReadAgainOnceInTheBackground()
        {
            var clock = new Clock();
            var store = new Store();
            using var cache = new AddonDefinitionCache(Options(), store.Scopes(), clock);
            using var request = new CancellationTokenSource();
            var old = Held("weight");

            cache.Set(Organisation, Widget, old);
            clock.Advance(TimeSpan.FromSeconds(29));

            cache.GetAsync(Organisation, Widget, NoRead, request.Token).Result.Should().Be(new AddonDefinitionLookup(old, AddonDefinitionWait.None));
            store.Reads.Should().Be(0, "a value within its lifetime is read from nowhere");

            clock.Advance(TimeSpan.FromSeconds(2));

            // The request is answered before the repository is: it waits for nothing.
            var stale = cache.GetAsync(Organisation, Widget, NoRead, request.Token);

            stale.IsCompletedSuccessfully.Should().BeTrue("a request never waits for a value the cache holds");
            stale.Result.Should().Be(new AddonDefinitionLookup(old, AddonDefinitionWait.None));

            await store.AskedAsync();

            // While that read is under way every request is answered the same way, and none starts another.
            for (var again = 0; again < 20; again++)
                cache.GetAsync(Organisation, Widget, NoRead, request.Token).Result.Definitions.Should().BeSameAs(old);

            store.Reads.Should().Be(1, "one read replaces the value, however many requests found it old");
            store.Tokens.Should().ContainSingle().Which.Should().NotBe(request.Token, "the read is not a request's: no request ends it");

            store.Answer("weight", "colour");
            await cache.RefreshedAsync(Organisation, Widget).WaitAsync(Patience);

            var fresh = cache.GetAsync(Organisation, Widget, NoRead, request.Token);

            fresh.IsCompletedSuccessfully.Should().BeTrue();
            fresh.Result.Waited.Should().Be(AddonDefinitionWait.None);
            fresh.Result.Definitions.Select(definition => definition.Path).Should().Equal("weight", "colour");
            store.Reads.Should().Be(1);

            // The new value has a lifetime of its own.
            clock.Advance(TimeSpan.FromSeconds(29));
            cache.GetAsync(Organisation, Widget, NoRead, request.Token).Result.Definitions.Should().BeSameAs(fresh.Result.Definitions);
            store.Reads.Should().Be(1);
        }

        [Fact]
        public async Task CallsThatMissTogether_ShareOneRead()
        {
            var store = new Store();
            using var cache = new AddonDefinitionCache(Options(), store.Scopes(), new Clock());

            var first = cache.GetAsync(Organisation, Widget, store.Read, CancellationToken.None).AsTask();

            await store.AskedAsync();

            var second = cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).AsTask();
            var third = cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).AsTask();

            first.IsCompleted.Should().BeFalse();
            second.IsCompleted.Should().BeFalse("the second call waits for the first one's read instead of sending its own");

            store.Answer("weight");

            var answers = await Task.WhenAll(first, second, third).WaitAsync(Patience);

            store.Reads.Should().Be(1, "two requests sent together must not both read");
            answers[0].Waited.Should().Be(AddonDefinitionWait.Read);
            answers[1].Waited.Should().Be(AddonDefinitionWait.Joined);
            answers[2].Waited.Should().Be(AddonDefinitionWait.Joined);
            answers[1].Definitions.Should().BeSameAs(answers[0].Definitions);
            answers[2].Definitions.Should().BeSameAs(answers[0].Definitions);

            // And the next call is answered from memory.
            cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).Result.Should().Be(new AddonDefinitionLookup(answers[0].Definitions, AddonDefinitionWait.None));
        }

        [Fact]
        public async Task ACallWhoseReaderGaveUp_ReadsForItself()
        {
            var store = new Store();
            using var cache = new AddonDefinitionCache(Options(), store.Scopes(), new Clock());
            using var leaving = new CancellationTokenSource();

            // The first request reads with its own token and is cancelled while the read is under way.
            var first = cache.GetAsync(Organisation, Widget, async token =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return [];
            }, leaving.Token).AsTask();

            var second = cache.GetAsync(Organisation, Widget, store.Read, CancellationToken.None).AsTask();

            leaving.Cancel();

            await FluentActions.Awaiting(() => first).Should().ThrowAsync<OperationCanceledException>();
            await store.AskedAsync();
            store.Answer("weight");

            var answer = await second.WaitAsync(Patience);

            answer.Waited.Should().Be(AddonDefinitionWait.Read, "the request that is still wanted asked for itself");
            answer.Definitions.Should().ContainSingle();
        }

        [Fact]
        public async Task AReadThatFails_FailsTheCallsWaitingForIt_AndTheNextCallReadsAgain()
        {
            var store = new Store();
            using var cache = new AddonDefinitionCache(Options(), store.Scopes(), new Clock());

            var first = cache.GetAsync(Organisation, Widget, store.Read, CancellationToken.None).AsTask();

            await store.AskedAsync();

            var second = cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).AsTask();

            store.Fail(new TimeoutException("the database did not answer"));

            await FluentActions.Awaiting(() => first).Should().ThrowAsync<TimeoutException>();
            await FluentActions.Awaiting(() => second).Should().ThrowAsync<TimeoutException>();
            cache.Count.Should().Be(0, "an entry that holds nothing does not stay");

            var third = cache.GetAsync(Organisation, Widget, store.Read, CancellationToken.None).AsTask();

            await store.AskedAsync();
            store.Answer("weight");

            (await third.WaitAsync(Patience)).Waited.Should().Be(AddonDefinitionWait.Read);
            store.Reads.Should().Be(2);
        }

        [Fact]
        public async Task AfterInvalidate_TheNextCallReads_AndAReadThatWasUnderWayStoresNothing()
        {
            var store = new Store();
            using var cache = new AddonDefinitionCache(Options(), store.Scopes(), new Clock());

            // A read begins, then a write here invalidates the entry: the read may have begun before the write.
            var before = cache.GetAsync(Organisation, Widget, store.Read, CancellationToken.None).AsTask();

            await store.AskedAsync();
            cache.Invalidate(Organisation, Widget);

            // The request after the write does not wait for the read that began before it.
            var after = cache.GetAsync(Organisation, Widget, store.Read, CancellationToken.None).AsTask();

            store.Reads.Should().Be(2, "the request after the write reads for itself");
            store.Answer("written");

            (await before.WaitAsync(Patience)).Waited.Should().Be(AddonDefinitionWait.Read);
            (await after.WaitAsync(Patience)).Waited.Should().Be(AddonDefinitionWait.Read);

            // What the cache holds is the later read's answer, and a further invalidation drops it at once.
            cache.TryGet(Organisation, Widget, out var held).Should().BeTrue();
            held.Should().BeSameAs(after.Result.Definitions);

            cache.Invalidate(Organisation, Widget);
            cache.TryGet(Organisation, Widget, out _).Should().BeFalse();
        }

        [Fact]
        public async Task AWriteThroughTheController_IsWhatTheNextRequestReads_AlsoWhileAnOlderValueWasStillServed()
        {
            var clock = new Clock();
            var stored = new List<AddonDefinitionDocument> { Document("weight") };
            var repository = new Mock<IAddonDefinitionRepository>(MockBehavior.Strict);

            repository.Setup(r => r.GetByEntitiesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => stored.ToList());
            repository.Setup(r => r.CreateAsync(It.IsAny<AddonDefinitionDocument>())).Callback((AddonDefinitionDocument definition) => stored.Add(definition)).Returns(Task.CompletedTask);
            repository.Setup(r => r.CommitAsync()).ReturnsAsync(1);

            using var cache = new AddonDefinitionCache(Options(), time: clock);
            var source = new AddonDefinitionSource(repository.Object, cache, SchemaBuild.Degraded);
            var controller = new AddonDefinitionController(repository.Object, new RequestContext { OrganizationId = Organisation }, SchemaBuild.Degraded, cache)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

            (await source.ForEntityAsync(Widget, Organisation, CancellationToken.None)).Select(definition => definition.Path).Should().Equal("weight");

            var answer = await controller.CreateAsync(new CreateAddonDefinitionRequest { Entity = Widget, Path = "colour", Kind = "string" }, CancellationToken.None);

            answer.Should().BeOfType<CreatedAtActionResult>();
            (await source.ForEntityAsync(Widget, Organisation, CancellationToken.None)).Select(definition => definition.Path).Should().Equal("weight", "colour");
        }

        [Fact]
        public async Task AValueNobodyAskedForDuringTenLifetimes_IsNotServed_TheRequestWaitsForTheRead()
        {
            var clock = new Clock();
            var store = new Store();
            using var cache = new AddonDefinitionCache(Options(), store.Scopes(), clock);
            var old = Held("weight");

            cache.Set(Organisation, Widget, old);
            clock.Advance(TimeSpan.FromSeconds(30 * AddonDefinitionCache.StaleFactor));
            cache.TryGet(Organisation, Widget, out _).Should().BeTrue("ten lifetimes old is still served");

            clock.Advance(TimeSpan.FromSeconds(1));
            cache.TryGet(Organisation, Widget, out _).Should().BeFalse();

            var request = cache.GetAsync(Organisation, Widget, store.Read, CancellationToken.None).AsTask();

            await store.AskedAsync();
            request.IsCompleted.Should().BeFalse();
            store.Answer("colour");

            var answer = await request.WaitAsync(Patience);

            answer.Waited.Should().Be(AddonDefinitionWait.Read);
            answer.Definitions.Select(definition => definition.Path).Should().Equal("colour");
        }

        [Fact]
        public async Task WithoutAScopeOfItsOwn_AValuePastItsLifetime_IsReadByTheRequest()
        {
            var clock = new Clock();
            var store = new Store();
            using var cache = new AddonDefinitionCache(Options(), time: clock);

            cache.Set(Organisation, Widget, Held("weight"));
            clock.Advance(TimeSpan.FromSeconds(31));

            var request = cache.GetAsync(Organisation, Widget, store.Read, CancellationToken.None).AsTask();

            await store.AskedAsync();
            store.Answer("colour");

            (await request.WaitAsync(Patience)).Should().Match<AddonDefinitionLookup>(lookup => lookup.Waited == AddonDefinitionWait.Read && lookup.Definitions.Single().Path == "colour");
        }

        [Fact]
        public async Task ABackgroundReadThatFails_KeepsTheValueHeld_AndIsTriedAgainLater()
        {
            var clock = new Clock();
            var store = new Store();
            using var cache = new AddonDefinitionCache(Options(), store.Scopes(), clock);
            var old = Held("weight");

            cache.Set(Organisation, Widget, old);
            clock.Advance(TimeSpan.FromSeconds(31));

            cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).Result.Definitions.Should().BeSameAs(old);
            await store.AskedAsync();
            store.Fail(new TimeoutException("the database did not answer"));
            await cache.RefreshedAsync(Organisation, Widget).WaitAsync(Patience);

            // The request is still answered, and the failed read is not repeated by every request that follows.
            for (var again = 0; again < 10; again++)
                cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).Result.Definitions.Should().BeSameAs(old);

            store.Reads.Should().Be(1);

            clock.Advance(TimeSpan.FromSeconds(6));
            cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).Result.Definitions.Should().BeSameAs(old);
            await store.AskedAsync();
            store.Reads.Should().Be(2, "after a pause the read is tried again");
            store.Answer("colour");
            await cache.RefreshedAsync(Organisation, Widget).WaitAsync(Patience);

            cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).Result.Definitions.Single().Path.Should().Be("colour");
        }

        [Fact]
        public void TheCache_IsBounded_AndKeepsWhatWasAskedForLast()
        {
            var clock = new Clock();
            using var cache = new AddonDefinitionCache(Options(), time: clock);
            var organisations = Enumerable.Range(0, AddonDefinitionCache.Capacity + 500).Select(_ => Guid.NewGuid()).ToList();

            foreach (var organisation in organisations)
            {
                cache.Set(organisation, Widget, []);
                clock.Advance(TimeSpan.FromMilliseconds(1));
            }

            cache.Count.Should().BeLessThanOrEqualTo(AddonDefinitionCache.Capacity);
            cache.TryGet(organisations[^1], Widget, out _).Should().BeTrue("the entry asked for last stays");
            cache.TryGet(organisations[0], Widget, out _).Should().BeFalse("the entry asked for longest ago left");
        }

        [Fact]
        public void TheCache_KeysPerOrganisationAndEntity()
        {
            using var cache = new AddonDefinitionCache(Options());
            var other = Guid.NewGuid();
            var mine = Held("weight");

            cache.Set(Organisation, Widget, mine);
            cache.Set(other, Widget, Held("colour"));
            cache.Set(Organisation, "probe.thing", []);

            cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).Result.Definitions.Should().BeSameAs(mine);
            cache.GetAsync(other, Widget, NoRead, CancellationToken.None).Result.Definitions.Single().Path.Should().Be("colour");
            cache.GetAsync(Organisation, "probe.thing", NoRead, CancellationToken.None).Result.Definitions.Should().BeEmpty();

            cache.Invalidate(Organisation, Widget);

            cache.TryGet(Organisation, Widget, out _).Should().BeFalse();
            cache.TryGet(other, Widget, out _).Should().BeTrue("another organisation's entry is another entry");
            cache.TryGet(Organisation, "probe.thing", out _).Should().BeTrue();
        }

        [Fact]
        public async Task TheHostsCache_ReadsInAScopeOfItsOwn()
        {
            var clock = new Clock();
            var store = new Store();

            // As Bootstrap registers it: the container hands the cache its scope factory.
            await using var services = new ServiceCollection()
                .AddSingleton(Options())
                .AddSingleton<TimeProvider>(clock)
                .AddSingleton(SchemaBuild.Degraded)
                .AddScoped(_ => store.Repository.Object)
                .AddSingleton<AddonDefinitionCache>()
                .BuildServiceProvider();
            var cache = services.GetRequiredService<AddonDefinitionCache>();

            cache.Set(Organisation, Widget, Held("weight"));
            clock.Advance(TimeSpan.FromSeconds(31));

            cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).IsCompletedSuccessfully.Should().BeTrue("the request is answered at once");
            await store.AskedAsync();
            store.Answer("colour");
            await cache.RefreshedAsync(Organisation, Widget).WaitAsync(Patience);

            cache.GetAsync(Organisation, Widget, NoRead, CancellationToken.None).Result.Definitions.Single().Path.Should().Be("colour");
        }

        [Fact]
        public async Task TheSource_SaysAReadToTheEngine_AndNothingWhenTheCacheAnswered()
        {
            var store = new Store();
            using var cache = new AddonDefinitionCache(Options(), store.Scopes(), new Clock());
            var service = new Mock<ICurrentService>();

            service.SetupGet(current => current.ServiceName).Returns("Probe");

            IReportingAddonDefinitionSource Source() => new AddonDefinitionSource(store.Repository.Object, cache, SchemaBuild.Degraded, service.Object);

            // Two requests at once, each with a source of its own, as two request scopes have.
            var reading = new AddonReadReport();
            var joining = new AddonReadReport();
            var first = Source().ForEntityAsync(Widget, Organisation, reading, CancellationToken.None).AsTask();

            await store.AskedAsync();

            var second = Source().ForEntityAsync(Widget, Organisation, joining, CancellationToken.None).AsTask();

            store.Answer("weight", "colour");
            await Task.WhenAll(first, second).WaitAsync(Patience);

            var read = reading.Reads.Should().ContainSingle().Subject;

            read.Collection.Should().Be("model_definition.addon_definition.probe");
            read.RoundTrips.Should().Be(1);
            read.Docs.Should().Be(2);
            read.Ended.Should().BeGreaterThanOrEqualTo(read.Started);
            joining.Reads.Should().ContainSingle().Which.RoundTrips.Should().Be(0, "it waited for the other request's read and sent none");
            joining.Reads[0].Collection.Should().Be(read.Collection);

            // The cache answers: nothing was waited for, nothing is said.
            var cached = new AddonReadReport();
            var third = Source().ForEntityAsync(Widget, Organisation, cached, CancellationToken.None);

            third.IsCompletedSuccessfully.Should().BeTrue();
            third.Result.Should().BeSameAs(first.Result);
            cached.Reads.Should().BeEmpty();
            store.Reads.Should().Be(1);
        }
    }
}
