using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Settings.Organization.Data;
using Simplic.OxS.Settings.Organization.Dto;
using Simplic.OxS.Settings.Organization.Exceptions;

namespace Simplic.OxS.Settings.Organization;

/// <summary>
/// Implementation of organization settings provider
/// </summary>
public class OrganizationSettingsProvider : IOrganizationSettingsProvider
{
    private readonly OrganizationSettingsRegistry registry;
    private readonly IOrganizationSettingRepository repository;
    private readonly ILogger<OrganizationSettingsProvider> logger;
    private readonly IRequestContext requestContext;

    /// <summary>
    /// Initialize provider
    /// </summary>
    public OrganizationSettingsProvider(
        OrganizationSettingsRegistry registry,
        IOrganizationSettingRepository repository,
        ILogger<OrganizationSettingsProvider> logger,
        IRequestContext requestContext)
    {
        this.registry = registry;
        this.repository = repository;
        this.logger = logger;
        this.requestContext = requestContext;
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
            (T?)result.DefaultValue);
    }

    /// <inheritdoc/>
    public async Task<OrganizationSettingResult> GetAsync(string internalName)
    {
        var get = registry.TryGet(internalName, out var definition);

        if (!get || definition == null)
            throw new SettingNotFoundException(internalName);

        var result = (await repository.GetByFilterAsync(new SettingFilter
        {
            InternalName = internalName
        })).FirstOrDefault();


        if (result is null)
            return new OrganizationSettingResult(
                definition.InternalName,
                definition.DisplayName,
                definition.DisplayKey,
                definition.DefaultValue,
                definition.DefaultValue,
                definition.ValueType.Name);

        var value = DeserializeValue(result.SerializedValue, definition.ValueType);

        return new OrganizationSettingResult(
            definition.InternalName,
            definition.DisplayName,
            definition.DisplayKey,
            value,
            definition.DefaultValue,
            definition.ValueType.Name);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<OrganizationSettingResult>> GetAllAsync()
    {
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
                        definition.ValueType.Name);
                })
                .ToList()
                .AsReadOnly();

            return results;
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
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            logger.LogError(ex, "Failed to update setting {InternalName} for organization {OrganizationId}",
                internalName, requestContext.OrganizationId);
            throw new PersistenceUnavailableException("Settings repository is currently unavailable.", ex);
        }
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