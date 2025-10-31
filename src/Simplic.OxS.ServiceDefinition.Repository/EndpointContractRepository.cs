using MongoDB.Driver;
using Simplic.OxS.Data.MongoDB;

namespace Simplic.OxS.ServiceDefinition.Repository;

/// <summary>
/// MongoDB implementation of organization setting repository
/// </summary>
public class EndpointContractRepository(IMongoContext context, IRequestContext requestContext) :
    MongoOrganizationRepositoryBase<EndpointContract, EndpointContractFilter>(context, requestContext),
    IEndpointContractRepository
{

    protected override IEnumerable<FilterDefinition<EndpointContract>> GetFilterQueries(EndpointContractFilter filter)
    {
        if (filter.Name is not null)
            yield return Builders<EndpointContract>.Filter.Eq(s => s.Name, filter.Name);

        if (filter.ProviderName is not null)
            yield return Builders<EndpointContract>.Filter.Eq(s => s.ProviderName, filter.ProviderName);

        foreach (var definition in base.GetFilterQueries(filter))
            yield return definition;
    }
    protected override string GetCollectionName() => "contract.endpoint";
}