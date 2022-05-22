using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Auth.Service
{
    public class UserService : IUserService
    {
        private readonly IUserRepository userRepository;

        public UserService(IUserRepository userRepository)
        {
            this.userRepository = userRepository;
        }

        public async Task<User> GetAsync(Guid id)
        {
            return await userRepository.GetAsync(id);
        }

        public async Task<User> GetAsync(string email)
        {
            return (await userRepository.GetByFilterAsync(new UserFilter
            {
                EMail = email
            })).FirstOrDefault();
        }

        public async Task<User> LoginAsync(string email, string password)
        {
            var user = await GetAsync(email);

            if (user == null)
                return null;

            var passwordHash = GetPasswordHash(user, password);

            if (passwordHash == user.Password)
            {
                // TODO: Set last login date

                return user;
            }
            else
            {
                // TODO: Increase login failed count
            }

            return null;
        }

        public async Task<User> RegisterAsync(string email, string password, string phoneNumber, string type)
        {
            if (!UserTypes.Contains(type))
                throw new ArgumentOutOfRangeException($"Invalid type: {type}");

            StringBuilder code = new StringBuilder();

            // Create new random number generator with some seed
            var rnd = new Random(Guid.NewGuid().GetHashCode());

            for (int i = 0; i < 6; i++)
                code.Append($"{rnd.Next(0, 9)}");

            var user = new User
            {
                Id = Guid.NewGuid(),
                EMail = email,
                PhoneNumber = phoneNumber,
                MailVerificationCode = code.ToString(),
                MailVerified = false,
                RegistrationDate = DateTime.Now,
                Salt = $"{Guid.NewGuid()}",
                Roles = new[] { type }
            };

            user.Password = GetPasswordHash(user, password);

            await userRepository.CreateAsync(user);
            await userRepository.CommitAsync();

            return user;
        }

        public async Task UpdateAsync(User user)
        {
            await userRepository.UpdateAsync(user);
            await userRepository.CommitAsync();
        }

        public async Task ChangePasswordAsync(User user, string passwordHash)
        {
            await userRepository.ChangePassword(user.Id, passwordHash);
        }

        public async Task SetLoginDevice(Guid id, string device)
        {
            await userRepository.SetLoginDevice(id, device);
        }

        public async Task SetMailVerifiedAsync(Guid id)
        {
            await userRepository.SetMailVerifiedAsync(id);
        }

        /// <summary>
        /// Create SHA256 hash
        /// </summary>
        /// <param name="rawData">raw data to hash</param>
        /// <returns>Computed hash</returns>
        private string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public string GetPasswordHash(User user, string password)
        {
            return ComputeSha256Hash($"{password}_{user.Salt}");
        }

        /// <summary>
        /// Gets all available user types
        /// </summary>
        public IList<string> UserTypes { get; } = new List<string>
        {
            "student", "company"
        };
    }
}
