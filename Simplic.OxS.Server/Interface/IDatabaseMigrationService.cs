using System.Threading.Tasks;

namespace Simplic.OxS.Server.Interface
{
    public interface IDatabaseMigrationService
    {
        Task Migrate();
    }
}
