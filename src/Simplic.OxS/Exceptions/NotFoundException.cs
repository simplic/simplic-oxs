namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>404 Not Found</c> response with an intentionally anonymous body —
/// it carries no information about the missing resource.
/// <para>
/// This is the preferred 404: use it for tenant-scoped reads (and generally) so that "the resource
/// does not exist", "it exists but is not yours" and "the route is invalid" stay indistinguishable,
/// preventing an attacker from probing for the existence of foreign ids. The identified
/// <see cref="ResourceNotFoundException"/> variant, which additionally publishes the resource type and
/// id, is deprecated and should only be used for administrative/owner-verified lookups.
/// </para>
/// </summary>
public class NotFoundException : OxSException
{
    private const string DefaultMessage = "The requested resource was not found.";

    /// <summary>
    /// Initializes a new <see cref="NotFoundException"/>.
    /// </summary>
    /// <param name="message">
    /// Optional client-safe message. Defaults to a generic phrase that reveals nothing about the
    /// resource. Do not pass resource identifiers here for tenant-scoped reads.
    /// </param>
    /// <param name="innerException">Optional inner exception.</param>
    public NotFoundException(string? message = null, Exception? innerException = null)
        : base(message ?? DefaultMessage, innerException)
    {
    }

    /// <inheritdoc/>
    public override int StatusCode => 404;

    /// <inheritdoc/>
    public override string? Title => "Not Found";

    /// <inheritdoc/>
    public override string? ProblemType => "urn:simplic-oxs:problem:not-found";
}
