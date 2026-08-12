namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>404 Not Found</c> response with an intentionally anonymous body —
/// it carries no information about the missing resource.
/// <para>
/// Use this for tenant-scoped reads where "the resource does not exist" and "it exists but is not
/// yours" must be indistinguishable, so an attacker cannot probe for the existence of foreign ids.
/// When the caller is allowed to know the concrete resource (e.g. an owner-verified or administrative
/// lookup), throw <see cref="ResourceNotFoundException"/> instead, which additionally
/// publishes the resource type and id.
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
