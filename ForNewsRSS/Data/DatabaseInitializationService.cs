using ForNewsRSS.Config;
using MongoDB.Driver;

namespace ForNewsRSS.Data
{
    public class DatabaseInitializationService : IHostedService
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<DatabaseInitializationService> _logger;
        private readonly IConfiguration _configuration;
        public DatabaseInitializationService(
          IMongoDatabase database,
          IConfiguration configuration,
          ILogger<DatabaseInitializationService> logger)
        {
            _database = database;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var sources = _configuration
                        .GetSection("RssSources")
                        .Get<List<SourceConfig>>()
                        ?? new List<SourceConfig>();

            if (!sources.Any())
            {
                _logger.LogWarning("No RSS sources found in configuration. Skipping index creation.");
                return;
            }

            foreach (var sourceName in sources)
            {
                var collection = _database.GetCollection<NewsItem>($"News_{sourceName.Name}");

                var indexKeys = Builders<NewsItem>.IndexKeys.Ascending(item => item.Link);
                var options = new CreateIndexOptions { Unique = true, Name = "unique_link" };
                var model = new CreateIndexModel<NewsItem>(indexKeys, options);

                try
                {
                    await collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
                    _logger.LogInformation("Unique index on Link created for collection: {Collection}", collection.CollectionNamespace.CollectionName);
                }
                catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict" || ex.CodeName == "IndexExists")
                {
                    _logger.LogInformation("Unique index already exists for {Collection}", collection.CollectionNamespace.CollectionName);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
