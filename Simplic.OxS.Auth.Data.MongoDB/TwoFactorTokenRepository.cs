using MongoDB.Driver;
using Simplic.OxS.Data.MongoDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Auth.Data.MongoDB
{
    public class TwoFactorTokenRepository : MongoRepositoryBase<Guid, TwoFactorToken, TwoFactorTokenFilter>, ITwoFactorTokenRepository
    {
        public TwoFactorTokenRepository(IMongoContext context) : base(context)
        {

        }

        protected override string GetCollectionName() => "simplic.oxs.two_factor_token";
    }
}
