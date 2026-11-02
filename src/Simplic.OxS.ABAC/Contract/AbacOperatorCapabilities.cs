namespace Simplic.OxS.ABAC.Contract;

/// <summary>
/// Declares which backends support a specific DSL operator.
/// Registered in <see cref="IAbacCompileContract.OperatorCapabilities"/>.
/// The compiler rejects (or falls back) when a required backend does not support the operator.
/// </summary>
public class AbacOperatorCapabilities
{
    /// <summary>DSL operator token (e.g. <c>$eq</c>, <c>$contains</c>, <c>$in</c>).</summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary><c>true</c> when the operator can be translated to a MongoDB query filter.</summary>
    public bool SupportsMongoDb { get; set; }

    /// <summary><c>true</c> when the operator can be translated to a search-index query.</summary>
    public bool SupportsSearch { get; set; }

    /// <summary>
    /// <c>true</c> when the operator can be used for SignalR broadcast filtering
    /// (in-process subject evaluation only, no DB translation required).
    /// </summary>
    public bool SupportsSignalR { get; set; }
}
