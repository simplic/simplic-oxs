using MongoDB.Driver;
using Simplic.OxS.Data.MongoDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Auth.Data.MongoDB
{
    public class UserRepository : MongoRepositoryBase<Guid, User, UserFilter>, IUserRepository
    {
        public UserRepository(IMongoContext context) : base(context)
        {

        }

        public async Task ChangePassword(Guid id, string password)
        {
            await Initialize();

            var filter = Builders<User>.Filter.Eq(x => x.Id, id);
            var update = Builders<User>.Update.Set(x => x.Password, password);
            await Collection.UpdateOneAsync(filter, update);
        }

        public async Task SetLoginDevice(Guid id, string device)
        {
            await Initialize();

            var filter = Builders<User>.Filter.Eq(x => x.Id, id);
            var update = Builders<User>.Update.Set(x => x.LoginDevice, device);
            await Collection.UpdateOneAsync(filter, update);
        }

        public async Task SetMailVerifiedAsync(Guid id)
        {
            await Initialize();

            var filter = Builders<User>.Filter.Eq(x => x.Id, id);
            var update = Builders<User>.Update.Set(x => x.MailVerified, true);
            await Collection.UpdateOneAsync(filter, update);
        }

        protected override IEnumerable<FilterDefinition<User>> GetFilterQueries(UserFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.EMail))
                yield return Builders<User>.Filter.Eq(x => x.EMail, filter.EMail);

            foreach (var e in base.GetFilterQueries(filter))
                yield return e;
        }

        public async Task Migrate()
        {
            await Initialize();

            var indexOptions = new CreateIndexOptions();

            var indexKeys = Builders<User>.IndexKeys.Ascending(x => x.EMail);
            var indexModel = new CreateIndexModel<User>(indexKeys, indexOptions);
            await Collection.Indexes.CreateOneAsync(indexModel);
        }

        protected override string GetCollectionName() => "simplic.oxs.user";
    }
}
