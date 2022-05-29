using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Simplic.OxS.Auth
{
    public interface ITwoFactorTokenService
    {
        Task<TwoFactorToken> GetAsync(Guid id);
        Task<TwoFactorToken> CreateAsync([NotNull] IDictionary<string, string> payload, string action);
        Task DeleteAsync(Guid id);
    }
}
