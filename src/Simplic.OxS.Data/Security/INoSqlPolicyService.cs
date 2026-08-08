using MongoDB.Bson;

namespace Simplic.OxS.Data.Security
{
    /// <summary>
    /// Represents the base for a NoSQL policy service. Which converts, caches and provides NoSQL queries for specific actions.
    /// </summary>
    public interface INoSqlPolicyService
    {
        /// <summary>
        /// Get the NoSQL query for the given action. For example "oxs:<resource-urn>:read", etc.
        /// </summary>
        /// <param name="resourceUrnAndAction">oxs:resource-urn:action</param>
        /// <returns>BsonDocument for the query</returns>
        BsonDocument? GetRulesAsFilter(string resourceUrnAndAction);
    }
}
