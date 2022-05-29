using Simplic.OxS.Data;
using System;

namespace Simplic.OxS.Auth
{
    public interface ITwoFactorTokenRepository : IRepository<Guid, TwoFactorToken, TwoFactorTokenFilter>
    {

    }
}
