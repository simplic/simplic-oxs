using OxQL.Core.Binding;
using OxQL.Core.Engine;
using OxQL.Core.Models;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// The query engine of a host that is configured with no database (no <c>MongoDB</c> section),
/// where the engine's adapter is not registered. It lets the host construct the query routes, so
/// the ones that read no data answer (<c>GET /OxQL/health</c>); a query or an explain fails with
/// a message that names the missing section, which the routes answer as the engine's internal
/// error and write to the log.
/// </summary>
internal sealed class NoDatabaseQueryEngine : IQueryEngine
{
    /// <summary>What a query on such a host fails with.</summary>
    internal const string Message = "This service executes no query: it is configured with no database (the MongoDB section).";

    /// <inheritdoc/>
    public Task<QueryOutcome> ExecuteAsync(QueryRequest request, RequestContext context, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<ExplainOutcome> ExplainAsync(ExplainRequest request, RequestContext context, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Message);
}
