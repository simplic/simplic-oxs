using Microsoft.Extensions.Caching.Distributed;
using Simplic.OxS.ServiceDefinition;
using MongoDB.Driver;
using HotChocolate;

namespace Simplic.OxS.GrpcSample.Client;

/// <summary>
/// Mock implementation of IDistributedCache for demonstration purposes
/// </summary>
public class MockDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _cache = new();

    public byte[]? Get(string key)
    {
        _cache.TryGetValue(key, out var value);
        return value;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        return Task.FromResult(Get(key));
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        _cache[key] = value;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
        // No-op for mock
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mock implementation of IEndpointContractRepository for demonstration purposes
/// </summary>
public class MockEndpointContractRepository : IEndpointContractRepository
{
    public Task<IEnumerable<EndpointContract>> GetByFilterAsync(EndpointContractFilter filter)
    {
        // For demonstration, return a mock endpoint contract for the UserService
        if (filter.Name == "user.service" || filter.Name == "userservice")
        {
            var contract = new EndpointContract
            {
                Id = Guid.NewGuid(),
                Name = filter.Name,
                Endpoint = "[grpc]https://localhost:8443::user.UserService",
                OrganizationId = filter.OrganizationId ?? Guid.Empty,
                IsDeleted = false
            };
            return Task.FromResult(new List<EndpointContract> { contract }.AsEnumerable());
        }

        // Return empty collection for other contracts
        return Task.FromResult(Enumerable.Empty<EndpointContract>());
    }

    public Task<EndpointContract> SaveAsync(EndpointContract obj, bool updateTimeStamp = true)
    {
        return Task.FromResult(obj);
    }

    public Task DeleteAsync(Guid id)
    {
        return Task.CompletedTask;
    }

    public Task<EndpointContract?> GetAsync(Guid id)
    {
        return Task.FromResult<EndpointContract?>(null);
    }

    public Task<EndpointContract> GetAsync(Guid id, bool queryAllOrganizations = false)
    {
        return Task.FromResult<EndpointContract>(null);
    }

    public Task<IEnumerable<EndpointContract>> GetAllAsync()
    {
        return Task.FromResult(Enumerable.Empty<EndpointContract>());
    }

    public Task<IExecutable<EndpointContract>> GetCollection()
    {
        return Task.FromResult<IExecutable<EndpointContract>>(null);
    }

    public Task CreateAsync(EndpointContract entity)
    {
        return Task.CompletedTask;
    }

    public Task UpdateAsync(EndpointContract obj)
    {
        return Task.CompletedTask;
    }

    public Task UpsertAsync(EndpointContractFilter filter, EndpointContract entity)
    {
        return Task.CompletedTask;
    }

    public Task<int> CommitAsync()
    {
        return Task.FromResult(0);
    }

    public Task<IEnumerable<EndpointContract>> FindAsync(EndpointContractFilter predicate, int? skip, int? limit, string sortField = "", bool isAscending = true, Collation collation = null)
    {
        return Task.FromResult(Enumerable.Empty<EndpointContract>());
    }

    public Task<long> CountAsync(EndpointContractFilter predicate, Collation collation = null)
    {
        return Task.FromResult(0L);
    }

    public void Dispose()
    {
        // No-op for mock
    }
}