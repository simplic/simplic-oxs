using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Simplic.OxS.Data.MongoDB
{
    /// <summary>
    /// Mongo context
    /// </summary>
    public class MongoContext : IMongoContext
    {
        private IMongoDatabase database;
        private ConnectionSettings settings;
        private readonly List<Func<Task>> commands;
        private readonly IConfiguration configuration;
        private IDictionary<string, string> connectionStringCache = new Dictionary<string, string>();

        public MongoContext(IConfiguration configuration)
        {
            BsonDefaults.GuidRepresentation = GuidRepresentation.Standard;

            // Every command will be stored and it'll be processed at SaveChanges
            commands = new List<Func<Task>>();

            this.configuration = configuration;
            SetConfiguration("MongoDB");
        }

        /// <summary>
        /// Initialize context
        /// </summary>
        private void Initialize()
        {
            if (MongoClient != null)
                return;

            MongoClient = new MongoClient(settings.ConnectionString);
            database = MongoClient.GetDatabase(settings.Database);
        }

        /// <summary>
        /// Dispose context
        /// </summary>
        public void Dispose()
        {
            while (Session != null && Session.IsInTransaction)
                Thread.Sleep(TimeSpan.FromMilliseconds(100));

            GC.SuppressFinalize(this);
        }

        public void AddCommand(Func<Task> func)
        {
            commands.Add(func);
        }

        public async Task<int> SaveChangesAsync()
        {
            Initialize();

            if (EnableTransactions)
            {
                using (Session = await MongoClient.StartSessionAsync())
                {
                    Session.StartTransaction();

                    var commandTasks = commands.Select(c => c());

                    await Task.WhenAll(commandTasks);

                    await Session.CommitTransactionAsync();
                }
            }
            else
            {
                var commandTasks = commands.Select(c => c());
                await Task.WhenAll(commandTasks);
            }

            var count = commands.Count;
            commands.Clear();

            return commands.Count;
        }

        /// <summary>
        /// Gets or sets whether transactions are allowed or not
        /// </summary>
        public bool EnableTransactions { get; private set; } = false;

        /// <summary>
        /// Get mongodb collection
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="name">Collection name</param>
        /// <returns>Collection instance</returns>
        public IMongoCollection<T> GetCollection<T>(string name)
        {
            Initialize();
            return database.GetCollection<T>(name);
        }

        /// <summary>
        /// Set database configuration by name
        /// </summary>
        public void SetConfiguration(string configurationName)
        {
            settings = configuration.GetSection(configurationName)?.Get<ConnectionSettings>();
            MongoClient = null;
            Initialize();
        }

        /// <summary>
        /// Gets the mongo client instance
        /// </summary>
        public MongoClient MongoClient { get; private set; }

        /// <summary>
        /// Gets or sets the client session handle
        /// </summary>
        public IClientSessionHandle Session { get; set; }
    }
}
