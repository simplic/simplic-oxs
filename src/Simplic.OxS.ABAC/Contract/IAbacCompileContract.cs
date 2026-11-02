namespace Simplic.OxS.ABAC.Contract;

/// <summary>
/// The compile contract that the policy compiler (identity service) consults
/// when transforming raw JSON policies into compiled policies.
/// <para>
/// Implementors register the action catalog, field mappings, and operator capabilities.
/// Domain services contribute via pull requests; the compiler treats this contract as
/// the single authoritative source of compile-time knowledge.
/// </para>
/// <para>
/// A policy rule is only compiled when:
/// <list type="bullet">
///   <item>its action appears in <see cref="ActionCatalog"/> (or matches a wildcard entry), and</item>
///   <item>all referenced fields appear in <see cref="FieldMappings"/>, and</item>
///   <item>all referenced operators appear in <see cref="OperatorCapabilities"/> with at least one backend enabled.</item>
/// </list>
/// </para>
/// </summary>
public interface IAbacCompileContract
{
    /// <summary>
    /// Action catalog: maps action patterns to the allowed subject and resource fields.
    /// Key is the action pattern (exact or prefix-wildcard, e.g. <c>oxs:document:read</c> or <c>oxs:*</c>).
    /// </summary>
    IReadOnlyDictionary<string, AbacActionDescriptor> ActionCatalog { get; }

    /// <summary>
    /// Field-to-backend mappings for all resource fields that may appear in policy conditions.
    /// Key is the policy field name (e.g. <c>department</c>).
    /// </summary>
    IReadOnlyDictionary<string, AbacFieldMapping> FieldMappings { get; }

    /// <summary>
    /// Capability matrix: declares which DSL operators are supported by each backend.
    /// Key is the operator token (e.g. <c>$eq</c>, <c>$contains</c>).
    /// </summary>
    IReadOnlyDictionary<string, AbacOperatorCapabilities> OperatorCapabilities { get; }
}
