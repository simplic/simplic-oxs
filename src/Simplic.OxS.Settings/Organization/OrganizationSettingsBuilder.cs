namespace Simplic.OxS.Settings.Organization;

/// <summary>
/// Implementation of organization settings builder
/// </summary>
public class OrganizationSettingsBuilder : IOrganizationSettingsBuilder
{
    private readonly OrganizationSettingsRegistry registry;

    /// <summary>
    /// Initialize builder
    /// </summary>
    /// <param name="registry">Settings registry</param>
    public OrganizationSettingsBuilder(OrganizationSettingsRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc/>
    public IOrganizationSettingsBuilder AddSetting(IOrganizationSettingDefinition definition)
    {
        registry.Add(definition);
        return this;
    }

    /// <inheritdoc/>
    public IOrganizationSettingsBuilder AddSetting<TDefinition>() 
        where TDefinition : IOrganizationSettingDefinition, new()
    {
        return AddSetting(new TDefinition());
    }
}