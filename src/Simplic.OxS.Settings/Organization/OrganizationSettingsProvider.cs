using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Simplic.OxS.Settings.Abstractions;
using Simplic.OxS.Settings.Organization.Data;
using Simplic.OxS.Settings.Organization.Exceptions;

namespace Simplic.OxS.Settings.Organization;

/// <summary>
/// Implementation of organization settings provider with distributed caching
/// </summary>
public class OrganizationSettingsProvider : IOrganizationSettingsProvider
{
    private readonly OrganizationSettingsRegistry registry;
    private readonly IOrganizationSettingRepository repository;
    private readonly IDistributedCache distributedCache;
    private readonly ILogger<OrganizationSettingsProvider> logger;
    private readonly IRequestContext requestContext;
    private readonly string serviceName;

    // Cache configuration
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AllSettingsCacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initialize provider
    /// </summary>
    public OrganizationSettingsProvider(
        OrganizationSettingsRegistry registry,
        IOrganizationSettingRepository repository,
        IDistributedCache distributedCache,
        ILogger<OrganizationSettingsProvider> logger,
        IRequestContext requestContext,
        OrganizationSettingsConfiguration configuration)
    {
        this.registry = registry;
        this.repository = repository;
        this.distributedCache = distributedCache;
        this.logger = logger;
        this.requestContext = requestContext;
        this.serviceName = configuration.ServiceName;
    }

    /// <inheritdoc/>
    public async Task<OrganizationSettingResult<T>> GetAsync<TDefinition, T>()
        where TDefinition : OrganizationSettingDefinition<T>, new()
    {
        var definition = GetOrCreateDefinition<TDefinition>();
        var result = await GetAsync(definition.InternalName);

        return new OrganizationSettingResult<T>(
            result.InternalName,
            result.DisplayName,
            result.DisplayKey,
            (T?)result.Value,
            (T?)result.DefaultValue,
            result.Options);
    }

    /// <inheritdoc/>
    public async Task<OrganizationSettingResult> GetAsync(string internalName)
    {
        if (!registry.TryGet(internalName, out var definition) || definition == null)
            throw new SettingNotFoundException(internalName);

        var cacheKey = GetCacheKey(internalName);
        
        try
        {
            // Try to get from cache first
            var cachedJson = await distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cachedResult = JsonSerializer.Deserialize<CachedSettingValue>(cachedJson);
                if (cachedResult != null)
                {
                    logger.LogDebug("Retrieved setting {InternalName} from cache for organization {OrganizationId} in service {ServiceName}", 
                        internalName, requestContext.OrganizationId, serviceName);
                    
                    // Properly materialize the cached value to the correct type
                    var materializedValue = MaterializeCachedValue(cachedResult.Value, definition.ValueType);
                    
                    return new OrganizationSettingResult(
                        definition.InternalName,
                        definition.DisplayName,
                        definition.DisplayKey,
                        materializedValue,
                        definition.DefaultValue,
                        definition.ValueType.Name,
                        GetOptionsFromDefinition(definition));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve setting {InternalName} from cache, falling back to database", internalName);
        }

        // Cache miss or error - get from database
        var result = (await repository.GetByFilterAsync(new SettingFilter
        {
            InternalName = internalName
        })).FirstOrDefault();

        object? effectiveValue;
        if (result == null)
        {
            effectiveValue = definition.DefaultValue;
        }
        else
        {
            effectiveValue = DeserializeValue(result.SerializedValue, definition.ValueType);
        }

        // Cache the result
        await CacheSettingValueAsync(cacheKey, effectiveValue, DefaultCacheDuration);

        return new OrganizationSettingResult(
            definition.InternalName,
            definition.DisplayName,
            definition.DisplayKey,
            effectiveValue,
            definition.DefaultValue,
            definition.ValueType.Name,
            GetOptionsFromDefinition(definition));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<OrganizationSettingResult>> GetAllAsync()
    {
        var allCacheKey = GetAllSettingsCacheKey();
        
        try
        {
            // Try to get all settings from cache first
            var cachedJson = await distributedCache.GetStringAsync(allCacheKey);
            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cachedResults = JsonSerializer.Deserialize<List<OrganizationSettingResult>>(cachedJson);
                if (cachedResults != null)
                {
                    logger.LogDebug("Retrieved all settings from cache for organization {OrganizationId} in service {ServiceName}", 
                        requestContext.OrganizationId, serviceName);
                    return cachedResults.AsReadOnly();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve all settings from cache, falling back to database");
        }

        try
        {
            var overrides = await repository.GetAllAsync();
            var overrideMap = overrides.ToDictionary(o => o.InternalName, o => o);

            var results = registry.All
                .Select(definition =>
                {
                    overrideMap.TryGetValue(definition.InternalName, out var overrideEntity);

                    object? effectiveValue = overrideEntity != null
                        ? DeserializeValue(overrideEntity.SerializedValue, definition.ValueType)
                        : definition.DefaultValue;

                    return new OrganizationSettingResult(
                        definition.InternalName,
                        definition.DisplayName,
                        definition.DisplayKey,
                        effectiveValue,
                        definition.DefaultValue,
                        definition.ValueType.Name,
                        GetOptionsFromDefinition(definition));
                })
                .ToList();

            // Cache all settings
            await CacheAllSettingsAsync(allCacheKey, results, AllSettingsCacheDuration);

            return results.AsReadOnly();
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            logger.LogError(ex, "Failed to retrieve settings for organization {OrganizationId}", requestContext.OrganizationId);
            throw new PersistenceUnavailableException("Settings repository is currently unavailable.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<TDefinition, T>(T value)
        where TDefinition : OrganizationSettingDefinition<T>, new()
    {
        var definition = GetOrCreateDefinition<TDefinition>();
        await SetAsync(definition.InternalName, value!);
    }

    /// <inheritdoc/>
    public async Task SetAsync(string internalName, object value)
    {
        if (requestContext.OrganizationId == null || requestContext.OrganizationId == Guid.Empty)
            throw new InvalidOperationException("Organization context is not set.");

        if (!registry.TryGet(internalName, out var definition) || definition == null)
            throw new SettingNotFoundException(internalName);

        ValidateValue(definition, value);

        // Check if value is the same as current to avoid unnecessary work
        var current = await GetAsync(internalName);

        if (AreValuesEqual(current.Value, value))
        {
            logger.LogDebug("Setting {InternalName} for organization {OrganizationId} unchanged", internalName, requestContext.OrganizationId);
            return; // Idempotent - no change needed
        }

        var entity = new OrganizationSetting
        {
            OrganizationId = requestContext.OrganizationId.Value,
            InternalName = internalName,
            SerializedValue = SerializeValue(value),
            ValueType = definition.ValueType.AssemblyQualifiedName ?? definition.ValueType.Name,
        };

        try
        {
            var currentSetting = (await repository.GetByFilterAsync(new SettingFilter
            {
                InternalName = internalName
            })).FirstOrDefault();

            if (currentSetting == null)
            {
                entity.Id = Guid.NewGuid();
                await repository.CreateAsync(entity);
            }
            else
            {
                entity.Id = currentSetting.Id;
                await repository.UpdateAsync(entity);
            }
            await repository.CommitAsync();

            // Invalidate cache after successful update
            await InvalidateCacheAsync(internalName);

            logger.LogDebug("Updated setting {InternalName} for organization {OrganizationId} in service {ServiceName} and invalidated cache", 
                internalName, requestContext.OrganizationId, serviceName);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            logger.LogError(ex, "Failed to update setting {InternalName} for organization {OrganizationId}",
                internalName, requestContext.OrganizationId);
            throw new PersistenceUnavailableException("Settings repository is currently unavailable.", ex);
        }
    }

    /// <summary>
    /// Get cache key for a specific setting
    /// </summary>
    private string GetCacheKey(string internalName)
    {
        return $"org_setting:{serviceName}:{requestContext.OrganizationId}:{internalName}";
    }

    /// <summary>
    /// Get cache key for all settings
    /// </summary>
    private string GetAllSettingsCacheKey()
    {
        return $"org_settings_all:{serviceName}:{requestContext.OrganizationId}";
    }

    /// <summary>
    /// Materialize cached value from JsonElement to proper type
    /// </summary>
    private object? MaterializeCachedValue(object? cachedValue, Type expectedType)
    {
        if (cachedValue == null)
            return null;

        // If it's already the correct type, return as-is
        if (cachedValue.GetType() == expectedType)
            return cachedValue;

        // Handle JsonElement conversion (this happens when deserializing from cache)
        if (cachedValue is JsonElement jsonElement)
        {
            return DeserializeJsonElement(jsonElement, expectedType);
        }

        // Try to convert other types
        try
        {
            return Convert.ChangeType(cachedValue, expectedType);
        }
        catch
        {
            // If conversion fails, try JSON serialization round-trip
            var json = JsonSerializer.Serialize(cachedValue);
            return JsonSerializer.Deserialize(json, expectedType);
        }
    }

    /// <summary>
    /// Deserialize JsonElement to the specified type
    /// </summary>
    private object? DeserializeJsonElement(JsonElement jsonElement, Type expectedType)
    {
        if (jsonElement.ValueKind == JsonValueKind.Null)
            return null;

        // Handle nullable types
        var actualType = Nullable.GetUnderlyingType(expectedType) ?? expectedType;

        try
        {
            return actualType switch
            {
                Type t when t == typeof(bool) => jsonElement.GetBoolean(),
                Type t when t == typeof(int) => jsonElement.GetInt32(),
                Type t when t == typeof(long) => jsonElement.GetInt64(),
                Type t when t == typeof(decimal) => jsonElement.GetDecimal(),
                Type t when t == typeof(double) => jsonElement.GetDouble(),
                Type t when t == typeof(float) => jsonElement.GetSingle(),
                Type t when t == typeof(string) => jsonElement.GetString(),
                Type t when t == typeof(Guid) => jsonElement.GetGuid(),
                Type t when t == typeof(DateTime) => jsonElement.GetDateTime(),
                Type t when t == typeof(DateOnly) => DateOnly.FromDateTime(jsonElement.GetDateTime()),
                Type t when t == typeof(TimeOnly) => TimeOnly.FromTimeSpan(jsonElement.GetDateTime().TimeOfDay),
                Type t when t.IsEnum => Enum.Parse(actualType, jsonElement.GetString() ?? string.Empty),
                _ => JsonSerializer.Deserialize(jsonElement.GetRawText(), expectedType)
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize cached JsonElement to type {Type}, falling back to raw JSON deserialization", expectedType.Name);
            return JsonSerializer.Deserialize(jsonElement.GetRawText(), expectedType);
        }
    }

    /// <summary>
    /// Cache a setting value
    /// </summary>
    private async Task CacheSettingValueAsync(string cacheKey, object? value, TimeSpan duration)
    {
        try
        {
            var cachedValue = new CachedSettingValue { Value = value };
            var json = JsonSerializer.Serialize(cachedValue);
            
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            };
            
            await distributedCache.SetStringAsync(cacheKey, json, options);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache setting value for key {CacheKey}", cacheKey);
        }
    }

    /// <summary>
    /// Cache all settings
    /// </summary>
    private async Task CacheAllSettingsAsync(string cacheKey, List<OrganizationSettingResult> results, TimeSpan duration)
    {
        try
        {
            var json = JsonSerializer.Serialize(results);
            
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            };
            
            await distributedCache.SetStringAsync(cacheKey, json, options);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache all settings for key {CacheKey}", cacheKey);
        }
    }

    /// <summary>
    /// Invalidate cache for a specific setting and all settings
    /// </summary>
    private async Task InvalidateCacheAsync(string internalName)
    {
        try
        {
            var settingCacheKey = GetCacheKey(internalName);
            var allSettingsCacheKey = GetAllSettingsCacheKey();

            // Remove both individual setting cache and all settings cache
            await Task.WhenAll(
                distributedCache.RemoveAsync(settingCacheKey),
                distributedCache.RemoveAsync(allSettingsCacheKey)
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to invalidate cache for setting {InternalName}", internalName);
        }
    }

    /// <summary>
    /// Get options from setting definition if it supports options
    /// </summary>
    private static IReadOnlyList<SettingOption>? GetOptionsFromDefinition(IOrganizationSettingDefinition definition)
    {
        return definition is ISettingWithOptions optionsDef ? optionsDef.Options : null;
    }

    private TDefinition GetOrCreateDefinition<TDefinition>()
        where TDefinition : IOrganizationSettingDefinition, new()
    {
        var defType = typeof(TDefinition);
        var existing = registry.All.FirstOrDefault(d => d.GetType() == defType);

        if (existing != null)
            return (TDefinition)existing;

        // Fallback: create new instance (should be registered, but allows non-blocking behavior)
        var definition = new TDefinition();
        logger.LogWarning("Setting definition {DefinitionType} not registered, using fallback instance", defType.Name);
        return definition;
    }

    private static void ValidateValue(IOrganizationSettingDefinition definition, object value)
    {
        if (value == null)
        {
            // Check if the type allows null
            var actualType = Nullable.GetUnderlyingType(definition.ValueType) ?? definition.ValueType;
            if (actualType == definition.ValueType) // Not nullable
                throw new SettingValueNullException(definition.InternalName);
            return;
        }

        var valueType = value.GetType();
        var expectedType = definition.ValueType;

        // Handle nullable types
        var actualExpectedType = Nullable.GetUnderlyingType(expectedType) ?? expectedType;

        if (!actualExpectedType.IsAssignableFrom(valueType))
            throw new SettingTypeMismatchException(definition.InternalName, expectedType, valueType);
    }

    private string SerializeValue(object value)
    {
        return JsonSerializer.Serialize(value);
    }

    private object? DeserializeValue(string? json, Type type)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize(json, type);
    }

    private static bool AreValuesEqual(object? current, object? newValue)
    {
        if (current == null && newValue == null) return true;
        if (current == null || newValue == null) return false;

        return current.Equals(newValue);
    }
}

/// <summary>
/// Represents a cached setting value
/// </summary>
internal class CachedSettingValue
{
    public object? Value { get; set; }
}