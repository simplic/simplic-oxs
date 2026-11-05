namespace Simplic.OxS.ABAC.Ast;

/// <summary>
/// Discriminator for <see cref="ConditionNode"/> tree nodes.
/// </summary>
public enum ConditionNodeType
{
    /// <summary>A leaf comparison: field op value.</summary>
    Comparison,

    /// <summary>Logical AND of child nodes.</summary>
    And,

    /// <summary>Logical OR of child nodes.</summary>
    Or
}
