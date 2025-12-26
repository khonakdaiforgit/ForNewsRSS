using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using ForNewsRSS.RssProcessor;
using MongoDB.Driver;

namespace ForNewsRSS.Services
{
    public class RssBackgroundService : BackgroundService
    {
        private readonly ILogger<RssBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<SourceConfig> _sources;

        public RssBackgroundService(
            ILogger<RssBackgroundService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

            _sources = configuration
                .GetSection("RssSources")
                .Get<List<SourceConfig>>() ?? new List<SourceConfig>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RSS Aggregator started with {Count} sources.", _sources.Count);

            // اجرای اولیه فوری
            await ProcessAllSourcesAsync(stoppingToken);

            // تنظیم تایمر جداگانه برای هر منبع
            var timers = _sources.ToDictionary(
                s => s,
                s => new PeriodicTimer(s.FetchInterval)
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                var tasks = _sources.Select(async source =>
                {
                    if (await timers[source].WaitForNextTickAsync(stoppingToken))
                    {
                        await ProcessSourceAsync(source, stoppingToken);
                    }
                });

                await Task.WhenAll(tasks);
            }
        }

        private async Task ProcessAllSourcesAsync(CancellationToken ct)
        {
            var tasks = _sources.Select(s => ProcessSourceAsync(s, ct));
            await Task.WhenAll(tasks);
        }

        private async Task ProcessSourceAsync(SourceConfig source, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;

            var logger = sp.GetRequiredService<ILoggerFactory>()
                .CreateLogger($"RssProcessor_{source.Name}");

            var database = sp.GetRequiredService<IMongoDatabase>();
            var telegramService = sp.GetRequiredService<TelegramBotService>();

            var processor = CreateProcessor(logger, database, telegramService, source);

            try
            {
                await processor.ProcessAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing source {Source}", source.Name);
            }
        }

        private RssFeedProcessor CreateProcessor(
            ILogger logger,
            IMongoDatabase database,
            TelegramBotService telegramService,
            SourceConfig source)
        {
            return source.Name switch
            {
                "BBC" => new BBCRssProcessor(logger, database, telegramService, source),
                "CNN" => new CNNRssProcessor(logger, database, telegramService, source),
                "Guardian" => new GuardianRssProcessor(logger, database, telegramService, source),
                _ => new _DefaultRssProcessor(logger, database, telegramService, source)
            };
        }
    }
}