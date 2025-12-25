// File: ForNewsRSS/Abstract/RssFeedProcessor.cs
using ForNewsRSS.Config;
using MongoDB.Driver;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;
using static System.Collections.Specialized.NameObjectCollectionBase;

namespace ForNewsRSS.Abstract
{
    public abstract class RssFeedProcessor
    {
        protected readonly ILogger Logger;
        protected readonly IMongoCollection<NewsItem> NewsCollection;
        protected readonly TelegramBotService TelegramService;
        protected readonly SourceConfig Config;

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
        }

        public virtual async Task ProcessAsync(CancellationToken ct)
        {
            await NewsCollection.DeleteManyAsync(c => c != null);
            var potentialNews = new List<(string Link, SyndicationItem Item)>();
            var allNewLinks = new HashSet<string>();

            // Fetch from all RSS URLs defined in config
            foreach (var url in Config.RssUrls)
            {
                try
                {
                    using var reader = XmlReader.Create(url);
                    var feed = SyndicationFeed.Load(reader);

                    if (feed?.Items == null || !feed.Items.Any())
                    {
                        Logger.LogInformation("No items found in feed {Url} for source {Source}", url, Config.Name);
                        continue;
                    }

                    foreach (var item in feed.Items)
                    {
                        var link = item.Links.FirstOrDefault()?.Uri?.ToString()?.Trim();
                        if (string.IsNullOrEmpty(link))
                            continue;

                        if (allNewLinks.Add(link)) // فقط یک بار اضافه میشه حتی اگر در چند فید تکراری باشه
                        {
                            potentialNews.Add((link, item));
                        }
                    }

                    Logger.LogInformation("Processed feed {Url} - {Count} items found for {Source}", url, feed.Items.Count(), Config.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error reading RSS feed {Url} for source {Source}", url, Config.Name);
                }
            }

            if (!potentialNews.Any())
            {
                Logger.LogInformation("No new items to process for {Source}", Config.Name);
                return;
            }

            // چک کردن لینک‌های موجود در دیتابیس
            var existingLinks = await NewsCollection
                .Find(Builders<NewsItem>.Filter.In(n => n.Link, allNewLinks))
                .Project(n => n.Link)
                .ToListAsync(ct);

            var existingLinksSet = new HashSet<string>(existingLinks);

            // ساخت لیست آیتم‌های واقعاً جدید
            var newsToInsert = new List<NewsItem>();

            foreach (var (link, item) in potentialNews)
            {
                if (existingLinksSet.Contains(link))
                    continue; // تکراریه

                var newsItem = ParseItem(item, Config.Name);

                if (newsItem != null)
                    newsToInsert.Add(newsItem);
            }

            // درج دسته‌ای و ارسال به تلگرام
            if (newsToInsert.Any())
            {
                NewsCollection.InsertManyAsync(newsToInsert, cancellationToken: ct);
                SendToTelegramAsync(newsToInsert);
                Logger.LogInformation("{Count} new articles saved and sent for {Source}", newsToInsert.Count, Config.Name);
            }
            else
            {
                Logger.LogInformation("No new articles to save for {Source}", Config.Name);
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

        private async Task SendToTelegramAsync(List<NewsItem> toInsert)
        {
            foreach (var item in toInsert)
            {
                await TelegramService.SendNewsAsync(item, Config.TelegramChatId);
            }
        }
    }
}