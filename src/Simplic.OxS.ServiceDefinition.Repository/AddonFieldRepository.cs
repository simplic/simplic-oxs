using MongoDB.Driver;
using Simplic.OxS.Data.MongoDB;

namespace Simplic.OxS.ServiceDefinition.Repository;

/// <summary>
/// MongoDB implementation of the <see cref="IAddonFieldRepository"/>.
/// </summary>
public class AddonFieldRepository(IMongoContext context, IRequestContext requestContext) :
    MongoOrganizationRepositoryBase<AddonField, AddonFieldFilter>(context, requestContext),
    IAddonFieldRepository
{
    protected override IEnumerable<FilterDefinition<AddonField>> GetFilterQueries(AddonFieldFilter filter)
    {
        if (filter.ObjectName is not null)
            yield return Builders<AddonField>.Filter.Eq(s => s.ObjectName, filter.ObjectName);

        if (filter.PropertyName is not null)
            yield return Builders<AddonField>.Filter.Eq(s => s.PropertyName, filter.PropertyName);

        foreach (var definition in base.GetFilterQueries(filter))
            yield return definition;
    }

    protected override string GetCollectionName() => "model_definition.addon_field";

    /// <inheritdoc/>
    public async Task<IEnumerable<AddonField>> GetByObjectNameAsync(string objectName)
    {
        return await GetByFilterAsync(new AddonFieldFilter
        {
            ObjectName = objectName,
            IsDeleted = false
        });
    }
}
