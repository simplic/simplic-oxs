using MongoDB.Driver;
using Simplic.OxS.Data.MongoDB;
using Simplic.OxS.Settings.Organization;
using Simplic.OxS.Settings.Organization.Data;

namespace Simplic.OxS.Settings.Repository;

/// <summary>
/// MongoDB implementation of organization setting repository
/// </summary>
public class OrganizationSettingRepository(IMongoContext context, IRequestContext requestContext) :
    MongoOrganizationRepositoryBase<OrganizationSetting, SettingFilter>(context, requestContext),
    IOrganizationSettingRepository
{

    protected override IEnumerable<FilterDefinition<OrganizationSetting>> GetFilterQueries(SettingFilter filter)
    {
        if (filter.InternalName is not null)
            yield return Builders<OrganizationSetting>.Filter.Eq(s => s.InternalName, filter.InternalName);

        foreach (var definition in base.GetFilterQueries(filter))
            yield return definition;
    }
    protected override string GetCollectionName() => "settings";
}