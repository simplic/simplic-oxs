namespace Simplic.OxS.Settings.Organization;

/// <summary>
/// Registry for all organization setting definitions
/// </summary>
public class OrganizationSettingsRegistry
{
    private readonly Dictionary<string, IOrganizationSettingDefinition> definitions = new();

    /// <summary>
    /// Add a setting definition to the registry
    /// </summary>
    /// <param name="definition">Setting definition</param>
    /// <exception cref="InvalidOperationException">If setting already exists</exception>
    public void Add(IOrganizationSettingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definitions.ContainsKey(definition.InternalName))
            throw new InvalidOperationException($"Setting '{definition.InternalName}' is already registered.");

        definitions[definition.InternalName] = definition;
    }

    /// <summary>
    /// Get all registered setting definitions
    /// </summary>
    public IReadOnlyCollection<IOrganizationSettingDefinition> All
    {
        get
        {
            return definitions.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Try to get a setting definition by internal name
    /// </summary>
    /// <param name="internalName">Internal name</param>
    /// <param name="definition">Found definition</param>
    /// <returns>True if found</returns>
    public bool TryGet(string internalName, out IOrganizationSettingDefinition? definition)
    {
        return definitions.TryGetValue(internalName, out definition);
    }
}