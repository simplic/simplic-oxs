using Simplic.OxS.Data;
using Simplic.OxS.Settings.Organization.Data;

namespace Simplic.OxS.Settings.Organization;

/// <summary>
/// Repository interface for organization setting persistence
/// </summary>
public interface IOrganizationSettingRepository : IOrganizationRepository<Guid, OrganizationSetting, SettingFilter>
{
    /// <summary>
    /// Retrieves all organization settings.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation, containing a list of organization settings.</returns>
    Task<IEnumerable<OrganizationSetting>> GetAllAsync();
}