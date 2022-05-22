using Simplic.OxS.Server;

namespace Simplic.OxS.Auth.Server.Migration
{
    public class AuthMigrationService : IDatabaseMigrationService
    {
        private readonly IUserService userService;
        private readonly IUserRepository userRepository;

        public AuthMigrationService(IUserService userService, IUserRepository userRepository)
        {
            this.userService = userService;
            this.userRepository = userRepository;
        }

        public async Task Migrate()
        {
            await userRepository.Migrate();

            var user = await userService.GetAsync("admin_1@Simplic.OxS.de");

            if (user == null)
            {
                user = await userService.RegisterAsync("admin_1@simplic-ox.de", "J7&$TU'c+KQ%Yn=X", "+4915124145570", "student");
                user.MailVerified = true;
                user.MailVerificationCode = $"{Guid.NewGuid()}";
                user.Roles = new[] { "admin" }; // Reset roles

                await userService.UpdateAsync(user);
            }
        }
    }
}
