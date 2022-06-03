using System.Threading.Tasks;

namespace Simplic.OxS.Server.Interface
{
    /// <summary>
    /// Interface that must be implemented for execute database migration on service startup.
    /// </summary>
    public interface IDatabaseMigrationService
    {
        /// <summary>
        /// Execute database migration.
        /// </summary>
        Task Migrate();
    }
}
