using Simplic.OxS.Settings.Organization.Dto;

namespace Simplic.OxS.Settings.Organization;

/// <summary>
/// Service interface for managing organization settings
/// </summary>
public interface IOrganizationSettingsProvider
{
    /// <summary>
    /// Get typed setting value for an organization
    /// </summary>
    /// <typeparam name="TDefinition">Setting definition type</typeparam>
    /// <typeparam name="T">Value type</typeparam>
    /// <returns>Typed setting result</returns>
    Task<OrganizationSettingResult<T>> GetAsync<TDefinition, T>()
        where TDefinition : OrganizationSettingDefinition<T>, new();

    /// <summary>
    /// Get setting by internal name
    /// </summary>
    /// <param name="internalName">Setting internal name</param>
    /// <returns>Setting result</returns>
    Task<OrganizationSettingResult> GetAsync(string internalName);

    /// <summary>
    /// Get all settings for an organization
    /// </summary>
    /// <returns>Collection of setting results</returns>
    Task<IReadOnlyCollection<OrganizationSettingResult>> GetAllAsync();

    /// <summary>
    /// Set typed setting value for an organization
    /// </summary>
    /// <typeparam name="TDefinition">Setting definition type</typeparam>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="value">New value</param>
    Task SetAsync<TDefinition, T>(T value)
        where TDefinition : OrganizationSettingDefinition<T>, new();

    /// <summary>
    /// Set setting value by internal name
    /// </summary>
    /// <param name="internalName">Setting internal name</param>
    /// <param name="value">New value</param>
    Task SetAsync(string internalName, object value);
}