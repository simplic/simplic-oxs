using System.Threading;
using System.Threading.Tasks;

namespace Simplic.OxS.Data.MongoDB
{
    /// <summary>
    /// Transaction service or the mongo db.
    /// </summary>
    public class MongoDbTransactionService : ITransactionService
    {
        private readonly MongoContext context;

        /// <summary>
        /// Initilizes a new instance of mongo database transaction service.
        /// </summary>
        /// <param name="context">Mongo database context.</param>
        public MongoDbTransactionService(IMongoContext context)
        {
            this.context = (MongoContext)context;
        }

        /// <summary>
        /// Initilizes a new instance of mongo db transaction service.
        /// </summary>
        /// <param name="context">Mongo database context.</param>
        /// <param name="configurationKey">Database configuration key.</param>
        public MongoDbTransactionService(IMongoContext context, string configurationKey)
        {
            this.context = (MongoContext)context;
            context.SetConfiguration(configurationKey);
        }
    
        /// <inheritdoc/>
        public async Task AbortAsync(ITransaction transaction, CancellationToken ct = default)
        {
            if (transaction is MongoTransaction mongoTransaction)
                await mongoTransaction.Session.AbortTransactionAsync(ct);
        }

        /// <inheritdoc/>
        public async Task CommitAsync(ITransaction transaction, CancellationToken ct = default)
        {
            if (transaction is MongoTransaction mongoTransaction)
                await mongoTransaction.Session.CommitTransactionAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<ITransaction> CreateAsync(CancellationToken ct = default)
        {
            var transaction = new MongoTransaction
            {
                Session = await context.MongoClient.StartSessionAsync(cancellationToken: ct)
            };

            transaction.Session.StartTransaction();

            return transaction;
        }
    }
}
