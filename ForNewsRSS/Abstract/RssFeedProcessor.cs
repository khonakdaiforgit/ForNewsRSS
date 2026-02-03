using ForNewsRSS.Config;
using ForNewsRSS.Entities;
using ForNewsRSS.Services;
using MongoDB.Driver;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;

namespace ForNewsRSS.Abstract
{
    public abstract class RssFeedProcessor
    {
        protected readonly ILogger Logger;
        protected readonly IMongoCollection<NewsItem> NewsCollection;
        protected readonly TelegramBotService TelegramService;
        protected readonly SourceConfig Config;

        protected readonly IMongoCollection<ProcessLog> ProcessLogCollection;
        protected readonly IMongoCollection<TelegramErrorLog> ErrorLogCollection;

        protected RssFeedProcessor(
            ILogger logger,
            IMongoDatabase database,
            TelegramBotService telegramService,
            SourceConfig config)
        {
            Logger = logger;
            Config = config;
            TelegramService = telegramService;
            NewsCollection = database.GetCollection<NewsItem>($"News_{config.Name}");

            ProcessLogCollection = database.GetCollection<ProcessLog>(nameof(ProcessLog));
            ErrorLogCollection = database.GetCollection<TelegramErrorLog>(nameof(TelegramErrorLog));
        }

        public virtual async Task ProcessAsync(CancellationToken ct)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                Logger.LogInformation("Starting processing for source {Source}", Config.Name);

                int totalFetched = 0;
                int newInserted = 0;
                int sentToTelegram = 0;
                int failedToSend = 0;

                var potentialNews = new List<(string Link, SyndicationItem Item)>();
                var allNewLinks = new HashSet<string>();

                foreach (var url in Config.RssUrls)
                {
                    try
                    {
                        Logger.LogDebug("Fetching RSS feed: {Url}", url);

                        using var reader = XmlReader.Create(url);
                        var feed = SyndicationFeed.Load(reader);

                        if (feed?.Items == null || !feed.Items.Any())
                        {
                            Logger.LogInformation("No items found in feed {Url} for source {Source}", url, Config.Name);
                            continue;
                        }

                        int itemCount = feed.Items.Count();

                        Logger.LogInformation("Successfully loaded {Count} items from {Url} for {Source}", itemCount, url, Config.Name);

                        foreach (var item in feed.Items)
                        {
                            var link = item.Links.FirstOrDefault()?.Uri?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(link))
                            {
                                Logger.LogWarning("Item without link skipped in feed {Url}", url);
                                continue;
                            }

                            if (allNewLinks.Add(link))
                            {
                                potentialNews.Add((link, item));
                            }
                        }
                    }
                    catch (XmlException xmlEx)
                    {
                        Logger.LogError(xmlEx, "XML parsing error in RSS feed {Url} for source {Source}", url, Config.Name);
                    }
                    catch (HttpRequestException httpEx)
                    {
                        Logger.LogError(httpEx, "Network error while fetching RSS feed {Url} for source {Source}", url, Config.Name);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Unexpected error reading RSS feed {Url} for source {Source}", url, Config.Name);
                    }
                }

                Logger.LogInformation("Total unique potential news items collected: {Count} (from {Fetched} total fetched items)",
                    potentialNews.Count, totalFetched);

                if (!potentialNews.Any())
                {
                    Logger.LogInformation("No potential new items to process for {Source}. Finishing early.", Config.Name);

                    await SaveProcessLog(startTime, totalFetched, 0, 0, 0, "No new items");
                    return;
                }

                Logger.LogDebug("Checking {Count} links against existing database entries", allNewLinks.Count);

                var existingLinks = await NewsCollection
                    .Find(Builders<NewsItem>.Filter.In(n => n.Link, allNewLinks))
                    .Project(n => n.Link)
                    .ToListAsync(ct);

                var existingLinksSet = new HashSet<string>(existingLinks);
                int duplicatesFound = potentialNews.Count - (potentialNews.Count - existingLinksSet.Count);

                Logger.LogInformation("Found {Duplicates} duplicate links already in database. {NewCount} truly new items.",
                    duplicatesFound, potentialNews.Count - duplicatesFound);

                var newsToInsert = new List<NewsItem>();

                foreach (var (link, item) in potentialNews)
                {
                    if (existingLinksSet.Contains(link))
                        continue;

                    var newsItem = ParseItem(item, Config.Name);
                    if (newsItem != null)
                    {
                        newsToInsert.Add(newsItem);
                    }
                    else
                    {
                        Logger.LogWarning("Failed to parse item with link: {Link}", link);
                    }
                }

                newInserted = newsToInsert.Count;

                if (newInserted == 0)
                {
                    Logger.LogInformation("No truly new valid items to insert for {Source}.", Config.Name);
                    await SaveProcessLog(startTime, totalFetched, 0, 0, 0, "No valid new items after parsing");
                    return;
                }

       
                try
                {
                    await NewsCollection.InsertManyAsync(newsToInsert, cancellationToken: ct);
                    Logger.LogInformation("Successfully inserted {Count} new items into database", newInserted);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to insert news items into MongoDB for source {Source}", Config.Name);

                    await SaveProcessLog(startTime, totalFetched, 0, 0, 0, "Insert failed: " + ex.Message);
                    return;
                }
                totalFetched += newInserted;

                Logger.LogInformation("Starting to send {Count} new items to Telegram (ChatId: {ChatId})", newInserted, Config.TelegramChatId);

                var (successful, failed) = await SendToTelegramAsync(newsToInsert);

                sentToTelegram = successful;
                failedToSend = failed;

                if (failed > 0)
                {
                    Logger.LogWarning("{Failed} out of {Total} items failed to send to Telegram for {Source}", failed, newInserted, Config.Name);
                }
                else
                {
                    Logger.LogInformation("All {Count} items successfully sent to Telegram", newInserted);
                }

                var duration = DateTime.UtcNow - startTime;
                await SaveProcessLog(startTime, totalFetched, newInserted, sentToTelegram, failedToSend, $"Completed in {duration.TotalSeconds:F1}s");

                Logger.LogInformation("Processing completed for {Source} in {Duration:F1} seconds. Fetched: {Fetched}, New: {New}, Sent: {Sent}, Failed: {Failed}",
                    Config.Name, duration.TotalSeconds, totalFetched, newInserted, sentToTelegram, failedToSend);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Critical error in processing {Source}", Config.Name);
            }

        }

        protected async Task SaveProcessLog(DateTime startTime, int fetched, int inserted, int sent, int failed, string notes)
        {
            var processLog = new ProcessLog
            {
                SourceName = Config.Name,
                ExecutionTime = startTime,
                TotalFetched = fetched,
                NewInserted = inserted,
                SentToTelegram = sent,
                FailedToSend = failed,
                Notes = notes
            };

            try
            {
                await ProcessLogCollection.InsertOneAsync(processLog);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to save ProcessLog for source {Source}", Config.Name);
            }
        }
        protected virtual NewsItem? ParseItem(SyndicationItem item, string sourceName)
        {
            var link = item.Links.FirstOrDefault()?.Uri?.ToString()?.Trim();
            if (string.IsNullOrEmpty(link))
                return null;

            string? imageUrl = ExtractImage(item);

            var publishDate = item.PublishDate != DateTimeOffset.MinValue
                ? item.PublishDate.DateTime
                : item.LastUpdatedTime != DateTimeOffset.MinValue
                    ? item.LastUpdatedTime.DateTime
                    : DateTime.UtcNow;

            return new NewsItem
            {
                Title = item.Title?.Text?.Trim() ?? "No title",
                Summary = item.Summary?.Text?.Trim() ?? string.Empty,
                Link = link,
                ImageUrl = imageUrl,
                PublishDate = publishDate,
                Source = sourceName
            };
        }

        protected virtual string? ExtractImage(SyndicationItem item)
        {
            // media:content
            var mediaContent = item.ElementExtensions
                .FirstOrDefault(e => e.OuterName == "content" &&
                                    e.OuterNamespace == "http://search.yahoo.com/mrss/");

            if (mediaContent != null)
            {
                return mediaContent.GetObject<XElement>().Attribute("url")?.Value;
            }

            // media:thumbnail
            var mediaThumbnail = item.ElementExtensions
                .FirstOrDefault(e => e.OuterName == "thumbnail" &&
                                    e.OuterNamespace == "http://search.yahoo.com/mrss/");

            if (mediaThumbnail != null)
            {
                return mediaThumbnail.GetObject<XElement>().Attribute("url")?.Value;
            }

            return null;
        }

        protected async Task<(int Successful, int Failed)> SendToTelegramAsync(List<NewsItem> toInsert)
        {
            int successful = 0;
            int failed = 0;

            foreach (var item in toInsert)
            {
                try
                {
                    await TelegramService.SendNewsAsync(item, Config.TelegramChatId);
                    await Task.Delay(1000 * 3, CancellationToken.None);
                    successful++;
                }
                catch (Exception ex)
                {
                    failed++;
                    var errorLog = new TelegramErrorLog
                    {
                        SourceName = Config.Name,
                        NewsLink = item.Link,
                        NewsTitle = item.Title,
                        NewsImageUrl = item.ImageUrl,
                        NewsSummary = item.Summary,
                        ErrorMessage = ex.Message,
                        Details = ex.StackTrace ?? string.Empty,
                        Timestamp = DateTime.UtcNow
                    };
                    await ErrorLogCollection.InsertOneAsync(errorLog);
                    Logger.LogError(ex, "Failed to send news to Telegram: {Title}", item.Title);
                }
            }

            return (successful, failed);
        }
    }
}
