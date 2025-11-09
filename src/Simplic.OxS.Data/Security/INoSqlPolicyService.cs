using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data.Security
{
    public interface INoSqlPolicyService
    {
        BsonDocument GetQuery(string action);
    }
}
