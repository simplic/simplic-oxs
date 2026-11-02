using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using Simplic.OxS.ServiceDefinition;
using Simplic.OxS.ServiceDefinition.Repository;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>The addon definition store: one collection per service, and the unique index over organisation, entity id and path.</summary>
    public sealed class AddonDefinitionRepositoryTests
    {
        [Theory]
        [InlineData("vehicle", "model_definition.addon_definition.vehicle")]
        [InlineData("Logistics", "model_definition.addon_definition.logistics")]
        [InlineData(" sequence-number ", "model_definition.addon_definition.sequence-number")]
        public void CollectionNameOf_CarriesTheLowerCasedServiceName(string service, string expected)
        {
            AddonDefinitionRepository.CollectionNameOf(service).Should().Be(expected);
        }

        [Fact]
        public void CollectionNameOf_TwoServicesNeverShareACollection()
        {
            AddonDefinitionRepository.CollectionNameOf("auth").Should().NotBe(AddonDefinitionRepository.CollectionNameOf("contact"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void CollectionNameOf_RefusesAHostThatNamesNoService(string? service)
        {
            FluentActions.Invoking(() => AddonDefinitionRepository.CollectionNameOf(service)).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void UniqueIndex_IsOverOrganisationEntityAndPathAmongTheRowsNotDeleted()
        {
            var index = AddonDefinitionRepository.UniqueIndex();
            var args = new RenderArgs<AddonDefinitionDocument>(BsonSerializer.SerializerRegistry.GetSerializer<AddonDefinitionDocument>(), BsonSerializer.SerializerRegistry);
            var options = index.Options.Should().BeOfType<CreateIndexOptions<AddonDefinitionDocument>>().Subject;

            index.Keys.Render(args).ToJson().Should().Be("{ \"OrganizationId\" : 1, \"Entity\" : 1, \"Path\" : 1 }");
            options.Unique.Should().BeTrue();
            options.Name.Should().Be("organization_entity_path_unique");
            options.PartialFilterExpression.Render(args).ToJson().Should().Be("{ \"IsDeleted\" : false }");
        }

        [Fact]
        public void IsDuplicateKey_ReadsOnlyTheUniqueIndexRefusal()
        {
            AddonDefinitionRepository.IsDuplicateKey(new MongoException("something else")).Should().BeFalse();
        }

        /// <summary>A repository over a collection whose index manager counts what it is asked and answers as told.</summary>
        private sealed class Store
        {
            public Store(string? service = null)
            {
                Service = service ?? "svc" + Guid.NewGuid().ToString("N");

                var indexes = new Mock<IMongoIndexManager<AddonDefinitionDocument>>();

                indexes.Setup(manager => manager.CreateOneAsync(It.IsAny<CreateIndexModel<AddonDefinitionDocument>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()))
                    .Returns(() =>
                    {
                        Asked++;

                        return Refuse is { } refusal ? Task.FromException<string>(refusal) : Task.FromResult(AddonDefinitionRepository.UniqueIndexName);
                    });

                var collection = new Mock<IMongoCollection<AddonDefinitionDocument>>();

                collection.SetupGet(each => each.Indexes).Returns(indexes.Object);
                collection.SetupGet(each => each.CollectionNamespace).Returns(() => new CollectionNamespace("lab", AddonDefinitionRepository.CollectionNameOf(Service)));

                var context = new Mock<Simplic.OxS.Data.MongoDB.IMongoContext>();

                context.Setup(each => each.GetCollection<AddonDefinitionDocument>(It.IsAny<string>())).Returns(collection.Object);

                var current = new Mock<ICurrentService>();

                current.SetupGet(each => each.ServiceName).Returns(() => Named ? Service : null!);
                Repository = new AddonDefinitionRepository(context.Object, Mock.Of<IRequestContext>(), current.Object, Log, Time);
            }

            public string Service { get; }

            public bool Named { get; set; } = true;

            public MongoException? Refuse { get; set; }

            public int Asked { get; private set; }

            public Clock Time { get; } = new();

            public Lines Log { get; } = new();

            public AddonDefinitionRepository Repository { get; }

            public string Namespace => "lab." + AddonDefinitionRepository.CollectionNameOf(Service);
        }

        private sealed class Clock : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class Lines : Microsoft.Extensions.Logging.ILogger<AddonDefinitionRepository>
        {
            public List<(Microsoft.Extensions.Logging.LogLevel Level, string Text)> Written { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                Written.Add((logLevel, formatter(state, exception)));
        }

        [Fact]
        public async Task EnsureIndexes_CreatesTheIndexOnceAndSaysItIsInPlace()
        {
            var store = new Store();

            AddonDefinitionRepository.IndexStateOf(store.Namespace).Should().BeNull("nothing is known before the first write");

            await store.Repository.EnsureIndexesAsync();
            await store.Repository.EnsureIndexesAsync();

            store.Asked.Should().Be(1);
            AddonDefinitionRepository.IndexStateOf(store.Namespace).Should().Be(new AddonDefinitionIndexState(true, null, null));
            AddonDefinitionRepository.IndexStates.Should().ContainKey(store.Namespace);
            store.Log.Written.Should().BeEmpty();
        }

        [Fact]
        public async Task EnsureIndexes_AnIndexThatCannotBeCreatedIsAskedForOnceLoggedOnceAndAskedForAgainOnlyAfterTheInterval()
        {
            var store = new Store { Refuse = new MongoException("not authorized on lab to execute command { createIndexes }") };

            // Every definition write asks; only the first sends the command the server refuses.
            for (var write = 0; write < 25; write++)
                await store.Repository.EnsureIndexesAsync();

            store.Asked.Should().Be(1, "a write does not pay a refused command each time");
            store.Log.Written.Should().ContainSingle("the failure is said once").Which.Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning);
            store.Log.Written[0].Text.Should().Contain(AddonDefinitionRepository.UniqueIndexName).And.Contain(store.Namespace).And.Contain("not at every write");

            var failed = AddonDefinitionRepository.IndexStateOf(store.Namespace)!;

            failed.Created.Should().BeFalse();
            failed.FailedAt.Should().Be(store.Time.Now);
            failed.Reason.Should().Contain("not authorized");

            // Short of the interval nothing is asked; past it one write asks again, and says nothing new at warning level.
            store.Time.Now += AddonDefinitionRepository.IndexRetryAfter - TimeSpan.FromSeconds(1);
            await store.Repository.EnsureIndexesAsync();
            store.Asked.Should().Be(1);

            store.Time.Now += TimeSpan.FromSeconds(2);
            await store.Repository.EnsureIndexesAsync();
            await store.Repository.EnsureIndexesAsync();
            store.Asked.Should().Be(2);
            store.Log.Written.Count(line => line.Level == Microsoft.Extensions.Logging.LogLevel.Warning).Should().Be(1);
            AddonDefinitionRepository.IndexStateOf(store.Namespace)!.FailedAt.Should().Be(store.Time.Now);

            // Once the cause is gone the next attempt creates it, without a restart, and no write asks after that.
            store.Refuse = null;
            store.Time.Now += AddonDefinitionRepository.IndexRetryAfter;
            await store.Repository.EnsureIndexesAsync();
            await store.Repository.EnsureIndexesAsync();

            store.Asked.Should().Be(3);
            AddonDefinitionRepository.IndexStateOf(store.Namespace).Should().Be(new AddonDefinitionIndexState(true, null, null));
            store.Log.Written[^1].Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Information);
        }

        [Fact]
        public async Task EnsureIndexes_ACommandThatDidNotReachAServerSaysNothingOfTheIndexAndTheNextWriteAsksAgain()
        {
            var endpoint = new System.Net.DnsEndPoint("localhost", 27017);
            var connection = new MongoDB.Driver.Core.Connections.ConnectionId(new MongoDB.Driver.Core.Servers.ServerId(new MongoDB.Driver.Core.Clusters.ClusterId(1), endpoint), 1);
            var store = new Store { Refuse = new MongoConnectionException(connection, "the connection was closed while the primary stepped down") };

            AddonDefinitionRepository.IsRefusal(store.Refuse).Should().BeFalse();
            AddonDefinitionRepository.IsRefusal(new MongoNotPrimaryException(connection, new MongoDB.Bson.BsonDocument("createIndexes", "x"), new MongoDB.Bson.BsonDocument("code", 10107))).Should().BeFalse();
            AddonDefinitionRepository.IsRefusal(new MongoCommandException(connection, "not authorized on lab to execute command { createIndexes }", new MongoDB.Bson.BsonDocument("createIndexes", "x"))).Should().BeTrue();
            AddonDefinitionRepository.IsRefusal(new MongoException("an index of that name exists with other options")).Should().BeTrue();

            // Three writes during the election: each asks, none concludes that the index cannot be created.
            for (var write = 0; write < 3; write++)
                await store.Repository.EnsureIndexesAsync();

            store.Asked.Should().Be(3, "a command that reached no server that could answer is asked again by the next write, not an hour later");
            AddonDefinitionRepository.IndexStateOf(store.Namespace).Should().BeNull("nothing is known of the index yet");
            store.Log.Written.Should().OnlyContain(line => line.Level == Microsoft.Extensions.Logging.LogLevel.Debug, "it is no warning: the unique index is not known to be missing");

            // The primary is back: the next write creates the index, and no write asks after that.
            store.Refuse = null;
            await store.Repository.EnsureIndexesAsync();
            await store.Repository.EnsureIndexesAsync();

            store.Asked.Should().Be(4);
            AddonDefinitionRepository.IndexStateOf(store.Namespace).Should().Be(new AddonDefinitionIndexState(true, null, null));
        }

        [Fact]
        public async Task AHostThatNamesNoServiceBuildsTheRepositoryAndFailsItsFirstUse()
        {
            // The repository is a dependency of the addon source every OxQL request holds: building it must not throw.
            var store = new Store { Named = false };

            (await FluentActions.Awaiting(() => store.Repository.EnsureIndexesAsync()).Should().ThrowAsync<InvalidOperationException>())
                .WithMessage("*the host names no service*");
        }
    }
}
