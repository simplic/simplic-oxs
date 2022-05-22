using Simplic.OxS.Data;
using System;
using System.Threading.Tasks;

namespace Simplic.OxS.Auth
{
    public interface IUserRepository : IRepository<Guid, User, UserFilter>
    {
        Task ChangePassword(Guid id, string password);

        Task SetLoginDevice(Guid id, string device);

        Task SetMailVerifiedAsync(Guid id);

        Task Migrate();
    }
}
