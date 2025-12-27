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

            // اجرای اولیه با staggered delay
            await ProcessAllSourcesStaggeredAsync(stoppingToken);

            // اجرای دوره‌ای: هر منبع در task جداگانه با loop خودش
            var tasks = _sources.Select(source => RunPeriodicProcessing(source, stoppingToken));
            await Task.WhenAll(tasks);
        }

        private async Task RunPeriodicProcessing(SourceConfig source, CancellationToken stoppingToken)
        {
            var delaySeconds = _sources.IndexOf(source) * 5; // staggered delay بر اساس ایندکس
            var initialDelay = TimeSpan.FromSeconds(delaySeconds);

            if (initialDelay > TimeSpan.Zero)
            {
                _logger.LogInformation("Applying initial delay of {Delay}s for source {Source}", initialDelay.TotalSeconds, source.Name);
                await Task.Delay(initialDelay, stoppingToken);
            }

            using var timer = new PeriodicTimer(source.FetchInterval); // using برای dispose خودکار

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await ProcessSourceAsync(source, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Periodic processing for {Source} canceled.", source.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in periodic processing for {Source}", source.Name);
            }
            // timer به خاطر using dispose می‌شود
        }
        private async Task ProcessAllSourcesStaggeredAsync(CancellationToken ct)
        {
            foreach (var (source, index) in _sources.Select((s, i) => (s, i)))
            {
                var delay = TimeSpan.FromSeconds(index * 5); // همان 15 ثانیه فاصله

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct);
                }

                _ = ProcessSourceAsync(source, ct); // fire and forget — یا await اگر بخوای صبر کنی
            }
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
                "ABCNews" => new ABCNewsRssProcessor(logger, database, telegramService, source),
                _ => new _DefaultRssProcessor(logger, database, telegramService, source)
            };
        }
    }
}