using MongoDB.Driver;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;

public class NewsRssBackgroundService : BackgroundService
{

    private readonly ILogger<NewsRssBackgroundService> _logger;
    private readonly IMongoCollection<NewsItem> _newsCollection;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(15); // Run every 15 minutes

    private readonly TelegramBotService _telegramBotService;

    private readonly List<(string Url, string SourceName)> _rssFeeds = new()
    {
        ("https://feeds.content.dowjones.io/public/rss/RSSWorldNews", "WSJ"),
        ("https://rss.nytimes.com/services/xml/rss/nyt/MostViewed.xml", "NYTimes"),
        ("https://feeds.bbci.co.uk/news/rss.xml", "BBC")
    };

    public NewsRssBackgroundService(
        ILogger<NewsRssBackgroundService> logger,
        IMongoDatabase database,
        TelegramBotService telegramBotService)
    {
        _logger = logger;
        _newsCollection = database.GetCollection<NewsItem>("News");
        _telegramBotService = telegramBotService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("News RSS Background Service started.");

        // First run immediately
        await FetchAndSaveNewsAsync(stoppingToken);

        using var timer = new PeriodicTimer(_period);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FetchAndSaveNewsAsync(stoppingToken);
        }
    }

    private async Task FetchAndSaveNewsAsync(CancellationToken ct)
    {
        // Collect all potential new links and items from all feeds first
        var potentialNews = new List<(string Link, SyndicationItem Item, string SourceName)>();
        var allNewLinks = new HashSet<string>();

        foreach (var (url, sourceName) in _rssFeeds)
        {
            try
            {
                using var reader = XmlReader.Create(url);
                var feed = SyndicationFeed.Load(reader);

                if (feed?.Items == null || !feed.Items.Any())
                {
                    _logger.LogInformation($"No items found in feed {sourceName}.");
                    continue;
                }

                foreach (var item in feed.Items)
                {
                    var link = item.Links.FirstOrDefault()?.Uri?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(link))
                        continue;

                    if (allNewLinks.Add(link)) // Adds only if not already present
                    {
                        potentialNews.Add((link, item, sourceName));
                    }
                }

                _logger.LogInformation($"Processed feed {sourceName} - {feed.Items.Count()} items found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading feed {sourceName}: {url}");
            }
        }

        if (!potentialNews.Any())
        {
            _logger.LogInformation("No new items to process across all feeds.");
            return;
        }

        // Single query: Find which of the new links already exist in the database
        var existingLinks = await _newsCollection
            .Find(Builders<NewsItem>.Filter.In(n => n.Link, allNewLinks))
            .Project(n => n.Link)
            .ToListAsync(ct);

        var existingLinksSet = new HashSet<string>(existingLinks);

        // Build list of only truly new items
        var newsToInsert = new List<NewsItem>();

        foreach (var (link, item, sourceName) in potentialNews)
        {
            if (existingLinksSet.Contains(link))
                continue; // Already exists → skip

            // Extract image (supports both media:content and media:thumbnail)
            string? imageUrl = null;

            var mediaContent = item.ElementExtensions
                .FirstOrDefault(e => e.OuterName == "content" &&
                                    e.OuterNamespace == "http://search.yahoo.com/mrss/");

            if (mediaContent != null)
            {
                imageUrl = mediaContent.GetObject<XElement>().Attribute("url")?.Value;
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                var mediaThumbnail = item.ElementExtensions
                    .FirstOrDefault(e => e.OuterName == "thumbnail" &&
                                        e.OuterNamespace == "http://search.yahoo.com/mrss/");

                if (mediaThumbnail != null)
                {
                    imageUrl = mediaThumbnail.GetObject<XElement>().Attribute("url")?.Value;
                }
            }

            // Optional: Upgrade BBC image quality from 240p to 1024p
            if (!string.IsNullOrEmpty(imageUrl) && imageUrl.Contains("/240/"))
            {
                imageUrl = imageUrl.Replace("/240/", "/1024/");
            }

            // Publish date with fallbacks
            var publishDate = item.PublishDate != DateTimeOffset.MinValue
                ? item.PublishDate.DateTime
                : item.LastUpdatedTime != DateTimeOffset.MinValue
                    ? item.LastUpdatedTime.DateTime
                    : DateTime.UtcNow;

            var newsItem = new NewsItem
            {
                Title = item.Title?.Text?.Trim() ?? "No title",
                Summary = item.Summary?.Text?.Trim() ?? string.Empty,
                Link = link,
                ImageUrl = imageUrl,
                PublishDate = publishDate,
                Source = sourceName
            };

            newsToInsert.Add(newsItem);
        }

        // Insert all new items in one batch
        if (newsToInsert.Any())
        {
            await _newsCollection.InsertManyAsync(newsToInsert, cancellationToken: ct);
            ExecuteSendToTelegramChanelAsync(newsToInsert);
            _logger.LogInformation($"{newsToInsert.Count} new articles saved to database.");
        }
        else
        {
            _logger.LogInformation("No new articles to save.");
        }
    }
    protected async Task ExecuteSendToTelegramChanelAsync(List<NewsItem> newsToInsert)
    {
        var orderedNews = newsToInsert
            .OrderBy(n => n.PublishDate)
            .ToList();

        foreach (var newsItem in orderedNews)
        {
            await _telegramBotService.SendNewsAsync(newsItem);
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}