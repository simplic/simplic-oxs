using Simplic.OxS.Data;
using Simplic.OxS.Settings.Organization.Data;

namespace Simplic.OxS.Settings.Organization;

/// <summary>
/// Repository interface for organization setting persistence
/// </summary>
public interface IOrganizationSettingRepository : IOrganizationRepository<Guid, OrganizationSetting, SettingFilter>
{
    Task<IEnumerable<OrganizationSetting>> GetAllAsync();
}