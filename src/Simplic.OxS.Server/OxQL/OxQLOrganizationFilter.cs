using MongoDB.Driver;
using OxQL.AspNetCore;
using OxQL.AspNetCore.Filtering;
using OxQL.Core.Filtering;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// Organization filter
/// </summary>
/// <param name="requestContext"></param>
public class OxQLOrganizationFilter(IRequestContext requestContext) : IOxQLQueryFilterProvider
{
    /// <summary>
    /// Create organization filter, to only allow to query data from the current organization
    /// </summary>
    public async ValueTask<IReadOnlyList<InjectedFilter>> GetFiltersAsync(OxQLFilterInjectionContext context, CancellationToken cancellationToken = default)
    {
        return [InjectedFilter.Create("OrganizationId", requestContext.OrganizationId)];
    }
}
