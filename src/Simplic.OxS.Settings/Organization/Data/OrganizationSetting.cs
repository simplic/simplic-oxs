using Simplic.OxS.Data;

namespace Simplic.OxS.Settings.Organization.Data;

/// <summary>
/// Entity for storing organization setting overrides
/// </summary>
public class OrganizationSetting : OrganizationDocumentBase
{
    /// <summary>
    /// Internal name of the setting
    /// </summary>
    public string InternalName { get; set; } = default!;

    /// <summary>
    /// Serialized setting value (JSON)
    /// </summary>
    public string? SerializedValue { get; set; }

    /// <summary>
    /// Type name for deserialization
    /// </summary>
    public string ValueType { get; set; } = default!;
}