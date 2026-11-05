using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Simplic.OxS.Data.MongoDB;
using Testcontainers.MongoDb;
using Testcontainers.Redis;
using Xunit;

namespace Simplic.OxS.Abac.IntegrationTest;

/// <summary>
/// Starts MongoDB and Redis containers once per test collection. When Docker is not available,
/// <see cref="Available"/> is <c>false</c> and all tests skip via <c>Skip.IfNot</c>.
/// </summary>
public sealed class AbacContainersFixture : IAsyncLifetime
{
    private MongoDbContainer? _mongo;
    private RedisContainer? _redis;

    /// <summary><c>true</c> when both containers are running.</summary>
    public bool Available { get; private set; }

    /// <summary>Reason why the containers are not available.</summary>
    public string? UnavailableReason { get; private set; }

    /// <summary>MongoDB connection string.</summary>
    public string MongoConnectionString => _mongo!.GetConnectionString();

    /// <summary>Redis connection string.</summary>
    public string RedisConnectionString => _redis!.GetConnectionString();

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        try
        {
            _mongo = new MongoDbBuilder("mongo:7.0").Build();
            _redis = new RedisBuilder("redis:7.2").Build();

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await Task.WhenAll(_mongo.StartAsync(timeout.Token), _redis.StartAsync(timeout.Token));
            Available = true;
        }
        catch (Exception ex)
        {
            Available = false;
            UnavailableReason = $"Docker/Testcontainers not available: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        if (_mongo != null)
        {
            try { await _mongo.DisposeAsync(); } catch (Exception) { /* containers may never have started */ }
        }

        if (_redis != null)
        {
            try { await _redis.DisposeAsync(); } catch (Exception) { /* containers may never have started */ }
        }
    }

    /// <summary>Creates a <see cref="MongoContext"/> for a fresh database.</summary>
    public MongoContext CreateMongoContext(string database)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDB:ConnectionString"] = MongoConnectionString,
                ["MongoDB:Database"] = database
            })
            .Build();

        return new MongoContext(configuration);
    }

    /// <summary>Creates a Redis-backed distributed cache with a unique instance prefix.</summary>
    public RedisCache CreateRedisCache()
        => new(new RedisCacheOptions
        {
            Configuration = RedisConnectionString,
            InstanceName = $"it-{Guid.NewGuid():N}:"
        });

    /// <summary>Skips the calling test when Docker is not available.</summary>
    public void SkipIfUnavailable() => Skip.IfNot(Available, UnavailableReason);
}

/// <summary>Collection definition sharing the containers between test classes.</summary>
[CollectionDefinition(Name)]
public class AbacContainersCollection : ICollectionFixture<AbacContainersFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "abac-containers";
}
