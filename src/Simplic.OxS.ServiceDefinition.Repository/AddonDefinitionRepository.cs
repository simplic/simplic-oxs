using MongoDB.Driver;
using Simplic.OxS.Data.MongoDB;

namespace Simplic.OxS.ServiceDefinition.Repository;

/// <summary>
/// MongoDB implementation of the <see cref="IAddonDefinitionRepository"/>.
/// </summary>
public class AddonDefinitionRepository(IMongoContext context, IRequestContext requestContext) :
    MongoOrganizationRepositoryBase<AddonDefinitionDocument, AddonDefinitionFilter>(context, requestContext),
    IAddonDefinitionRepository
{
    protected override IEnumerable<FilterDefinition<AddonDefinitionDocument>> GetFilterQueries(AddonDefinitionFilter filter)
    {
        if (filter.Entity is not null)
            yield return Builders<AddonDefinitionDocument>.Filter.Eq(s => s.Entity, filter.Entity);

        if (filter.Path is not null)
            yield return Builders<AddonDefinitionDocument>.Filter.Eq(s => s.Path, filter.Path);

        if (filter.Retired is { } retired)
            yield return Builders<AddonDefinitionDocument>.Filter.Eq(s => s.Retired, retired);

        foreach (var definition in base.GetFilterQueries(filter))
            yield return definition;
    }

    protected override string GetCollectionName() => "model_definition.addon_definition";

    /// <inheritdoc/>
    public async Task<IEnumerable<AddonDefinitionDocument>> GetByEntityAsync(string entity, Guid? organizationId = null)
    {
        return await GetByFilterAsync(new AddonDefinitionFilter
        {
            Entity = entity,
            OrganizationId = organizationId,
            IsDeleted = false,
        });
    }
}
