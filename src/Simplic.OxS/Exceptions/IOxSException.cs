namespace Simplic.OxS;

/// <summary>
/// Marks an exception as carrying an intended HTTP status code and a stable,
/// machine-readable error code.
/// <para>
/// The global exception handler in <c>Simplic.OxS.Server</c> uses this to turn the
/// exception into an RFC 7807 <c>ProblemDetails</c> response instead of an opaque 500.
/// Any exception that implements this interface is handled automatically — no
/// per-controller exception filter is required.
/// </para>
/// </summary>
public interface IOxSException
{
    /// <summary>
    /// The HTTP status code this exception should produce (e.g. 404, 409).
    /// </summary>
    int StatusCode { get; }

    /// <summary>
    /// Stable machine-readable error code in <c>snake_case</c> (e.g. <c>resource_not_found</c>).
    /// <para>
    /// Clients branch on this value, so treat it as part of the public API contract:
    /// never change an existing code, only add new ones.
    /// </para>
    /// </summary>
    string ErrorCode { get; }

    /// <summary>
    /// Additional members to surface on the <c>ProblemDetails</c> response.
    /// <para>
    /// Must contain only non-sensitive data — the contents are returned to the caller
    /// verbatim in every environment.
    /// </para>
    /// </summary>
    IReadOnlyDictionary<string, object?> ProblemExtensions { get; }
}
