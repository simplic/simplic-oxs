namespace Simplic.OxS.ABAC.Mapping;

/// <summary>Kind of a security field (§12.7).</summary>
public enum AbacFieldType
{
    /// <summary>Stored in <c>security.principals</c> as <c>{key}:{value}</c>.</summary>
    Principal = 0,

    /// <summary>Stored in <c>security.classification</c>.</summary>
    Classification = 1,

    /// <summary>Stored in <c>security.attributes.{key}</c>.</summary>
    Attribute = 2
}