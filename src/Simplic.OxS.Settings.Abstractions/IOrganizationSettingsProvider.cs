namespace Simplic.OxS.Settings.Abstractions;

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
    /// <param name="ct">Cancellation token</param>
    /// <returns>Typed setting result</returns>
    Task<OrganizationSettingResult<T>> GetAsync<TDefinition, T>(CancellationToken ct = default)
        where TDefinition : OrganizationSettingDefinition<T>, new();

    /// <summary>
    /// Get setting by internal name
    /// </summary>
    /// <param name="internalName">Setting internal name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Setting result</returns>
    Task<OrganizationSettingResult> GetAsync(string internalName, CancellationToken ct = default);

    /// <summary>
    /// Get all settings for an organization
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of setting results</returns>
    Task<IReadOnlyCollection<OrganizationSettingResult>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Set typed setting value for an organization
    /// </summary>
    /// <typeparam name="TDefinition">Setting definition type</typeparam>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="value">New value</param>
    /// <param name="ct">Cancellation token</param>
    Task SetAsync<TDefinition, T>(T value, CancellationToken ct = default)
        where TDefinition : OrganizationSettingDefinition<T>, new();

    /// <summary>
    /// Set setting value by internal name
    /// </summary>
    /// <param name="internalName">Setting internal name</param>
    /// <param name="value">New value</param>
    /// <param name="ct">Cancellation token</param>
    Task SetAsync(string internalName, object value, CancellationToken ct = default);
}