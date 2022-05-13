using System.Threading.Tasks;

namespace Simplic.OxS.Server
{
    public interface IDatabaseMigrationService
    {
        Task Migrate();
    }
}
