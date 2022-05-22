using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Simplic.OxS.Auth.Service
{
    public class TwoFactorTokenService : ITwoFactorTokenService
    {
        private readonly ITwoFactorTokenRepository twoFactorTokenRepository;

        public TwoFactorTokenService(ITwoFactorTokenRepository twoFactorTokenRepository)
        {
            this.twoFactorTokenRepository = twoFactorTokenRepository;
        }

        public async Task<TwoFactorToken> GetAsync(Guid id)
        {
            return await twoFactorTokenRepository.GetAsync(id);
        }

        public async Task<TwoFactorToken> CreateAsync([NotNull] IDictionary<string, string> payload, string action)
        {
            StringBuilder code = new StringBuilder();

            // Create new random number generator with some seed
            var rnd = new Random(Guid.NewGuid().GetHashCode());

            for (int i = 0; i < 6; i++)
                code.Append($"{rnd.Next(0, 9)}");

            var twoFactorToken = new TwoFactorToken
            {
                Id = Guid.NewGuid(),
                CreateDateTime = DateTime.Now,
                Payload = payload,
                Code = code.ToString(),
                Action = action
            };

            await twoFactorTokenRepository.CreateAsync(twoFactorToken);
            await twoFactorTokenRepository.CommitAsync();

            return twoFactorToken;
        }

        public async Task DeleteAsync(Guid id)
        {
            await twoFactorTokenRepository.DeleteAsync(id);
            await twoFactorTokenRepository.CommitAsync();
        }
    }
}
