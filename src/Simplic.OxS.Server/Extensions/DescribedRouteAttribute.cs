namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// Marks an action of this package that says everything of itself the OpenAPI document shows:
    /// its summary and description (<c>[EndpointSummary]</c>, <c>[EndpointDescription]</c>) and
    /// every status it answers with its body (<c>[ProducesResponseType]</c>). The document then
    /// carries those statuses and no other (<see cref="ProblemDetailsOperationFilter"/> adds none),
    /// and <see cref="DescribedRouteOperationFilter"/> adds what attributes of the framework cannot
    /// say: that an anonymous route needs no credentials, the headers of a conditional request,
    /// what <c>[Description]</c> says of a parameter, and an example of the request and of the
    /// answer. It changes nothing about how a request is handled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class DescribedRouteAttribute : Attribute
    {
        /// <summary>
        /// Whether the answer carries an <c>ETag</c> and the route answers 304 to a matching
        /// <c>If-None-Match</c>: the document then lists the request header and the response header.
        /// </summary>
        public bool Conditional { get; init; }

        /// <summary>An example of the request body as JSON text, or null for none.</summary>
        public string? RequestExample { get; init; }

        /// <summary>An example of the answer of a success (200 or 201) as JSON text, or null for none.</summary>
        public string? AnswerExample { get; init; }
    }
}
