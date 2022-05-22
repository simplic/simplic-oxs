using Simplic.OxS.Data;
using System;
using System.Threading.Tasks;

namespace Simplic.OxS.Auth
{
    public interface IUserService
    {
        Task<User> GetAsync(Guid id);

        Task<User> GetAsync(string email);

        Task<User> RegisterAsync(string email, string password, string phoneNumber, string type);

        Task<User> LoginAsync(string email, string password);

        IList<string> UserTypes { get; }

        string GetPasswordHash(User user, string password);

        Task ChangePasswordAsync(User user, string passwordHash);
        Task SetLoginDevice(Guid id, string device);
        Task SetMailVerifiedAsync(Guid id);
        Task UpdateAsync(User user);
    }
}
