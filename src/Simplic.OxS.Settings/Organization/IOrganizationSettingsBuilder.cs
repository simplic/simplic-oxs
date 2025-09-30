namespace Simplic.OxS.Settings.Organization;

/// <summary>
/// Builder interface for configuring organization settings
/// </summary>
public interface IOrganizationSettingsBuilder
{
    /// <summary>
    /// Add a setting definition instance
    /// </summary>
    /// <param name="definition">Setting definition</param>
    /// <returns>Builder for chaining</returns>
    IOrganizationSettingsBuilder AddSetting(IOrganizationSettingDefinition definition);
    
    /// <summary>
    /// Add a setting definition by type
    /// </summary>
    /// <typeparam name="TDefinition">Setting definition type</typeparam>
    /// <returns>Builder for chaining</returns>
    IOrganizationSettingsBuilder AddSetting<TDefinition>() 
        where TDefinition : IOrganizationSettingDefinition, new();
}