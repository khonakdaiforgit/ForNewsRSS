using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using ForNewsRSS.Services;
using MongoDB.Driver;
using System.Net;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;

namespace ForNewsRSS.RssProcessor
{
    public class DeutscheWelleRssProcessor : RssFeedProcessor
    {
        private static readonly XNamespace RdfNs = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";
        public DeutscheWelleRssProcessor(
            ILogger logger,
            IMongoDatabase database,
            TelegramBotService telegramService,
            SourceConfig config)
            : base(logger, database, telegramService, config)
        {
            // هیچ override خاصی لازم نیست — از منطق پایه استفاده می‌کنه
        }

        public override async Task ProcessAsync(CancellationToken ct)
        {
            try
            {
                //await NewsCollection.DeleteManyAsync(c => c != null);
                var startTime = DateTime.UtcNow;
                Logger.LogInformation("Starting processing for source {Source}", Config.Name);

                int totalFetched = 0;
                int newInserted = 0;
                int sentToTelegram = 0;
                int failedToSend = 0;

                var potentialNews = new List<(string Link, SyndicationItem Item)>();
                var allNewLinks = new HashSet<string>();

                // === مرحله ۱: فچ RSS ===
                foreach (var url in Config.RssUrls)
                {
                    try
                    {
                        XmlDocument doc = new XmlDocument();

                        // بعضی RSS ها بدون User-Agent خطا می‌دهند
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0");
                            string xml = wc.DownloadString(url);
                            doc.LoadXml(xml);
                        }

                        // تعریف Namespace ها
                        XmlNamespaceManager ns = new XmlNamespaceManager(doc.NameTable);
                        ns.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
                        ns.AddNamespace("rss", "http://purl.org/rss/1.0/");
                        ns.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");

                        // انتخاب آیتم‌ها
                        XmlNodeList items = doc.SelectNodes("//rss:item", ns);


                        if (items == null || items.Count==0)
                        {
                            Logger.LogInformation("No items found in RDF feed {Url}", url);
                            continue;
                        }

                        totalFetched += items.Count;
                        Logger.LogInformation("Loaded {Count} items from RDF feed {Url}", items.Count, url);

                        foreach (XmlNode itemElem in items)
                        {
                            string title = itemElem.SelectSingleNode("rss:title", ns)?.InnerText;
                            string link = itemElem.SelectSingleNode("rss:link", ns)?.InnerText;
                            string description = itemElem.SelectSingleNode("rss:description", ns)?.InnerText;
                            string date = itemElem.SelectSingleNode("dc:date", ns)?.InnerText;

                            if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(title))
                            {
                                Logger.LogWarning("Item missing title or link skipped in {Url}", url);
                                continue;
                            }

                            // ساخت SyndicationItem دستی برای سازگاری با بقیه کد
                            var syndItem = new SyndicationItem
                            {
                                Title = new TextSyndicationContent(title ?? "No title"),
                                Summary = new TextSyndicationContent(description ?? string.Empty),
                                PublishDate = ParseDcDate(date),
                            };

                            syndItem.Links.Add(new SyndicationLink(new Uri(link)));

                            if (allNewLinks.Add(link))
                            {
                                potentialNews.Add((link, syndItem));
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

                    // همچنان لاگ فرآیند را ذخیره کن (حتی اگر هیچ کاری انجام نشده)
                    await base.SaveProcessLog(startTime, totalFetched, 0, 0, 0, "No new items");
                    return;
                }

                // === مرحله ۲: چک تکراری بودن در دیتابیس ===
                Logger.LogDebug("Checking {Count} links against existing database entries", allNewLinks.Count);

                var existingLinks = await NewsCollection
                    .Find(Builders<NewsItem>.Filter.In(n => n.Link, allNewLinks))
                    .Project(n => n.Link)
                    .ToListAsync(ct);

                var existingLinksSet = new HashSet<string>(existingLinks);
                int duplicatesFound = potentialNews.Count - (potentialNews.Count - existingLinksSet.Count);

                Logger.LogInformation("Found {Duplicates} duplicate links already in database. {NewCount} truly new items.",
                    duplicatesFound, potentialNews.Count - duplicatesFound);

                // === مرحله ۳: پارس و ساخت آیتم‌های جدید ===
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

                // === مرحله ۴: ذخیره در دیتابیس ===
                //Logger.LogInformation("Inserting {Count} new news items into database for {Source}", newInserted);

                try
                {
                    await NewsCollection.InsertManyAsync(newsToInsert, cancellationToken: ct);
                    Logger.LogInformation("Successfully inserted {Count} new items into database", newInserted);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to insert news items into MongoDB for source {Source}", Config.Name);
                    // حتی در صورت خطا، لاگ فرآیند را ذخیره کن
                    await SaveProcessLog(startTime, totalFetched, 0, 0, 0, "Insert failed: " + ex.Message);
                    return;
                }
                totalFetched += newInserted;

                // === مرحله ۵: ارسال به تلگرام ===
                Logger.LogInformation("Starting to send {Count} new items to Telegram (ChatId: {ChatId})", newInserted, Config.TelegramChatId);

                var (successful, failed) = await base.SendToTelegramAsync(newsToInsert);

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

                // === مرحله نهایی: ذخیره لاگ فرآیند ===
                var duration = DateTime.UtcNow - startTime;
                await SaveProcessLog(startTime, totalFetched, newInserted, sentToTelegram, failedToSend, $"Completed in {duration.TotalSeconds:F1}s");

                Logger.LogInformation("Processing completed for {Source} in {Duration:F1} seconds. Fetched: {Fetched}, New: {New}, Sent: {Sent}, Failed: {Failed}",
                    Config.Name, duration.TotalSeconds, totalFetched, newInserted, sentToTelegram, failedToSend);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Critical error in processing {Source}", Config.Name);
                // اختیاری: rethrow نکن تا اپ ادامه دهد
            }
        }

        private DateTimeOffset ParseDcDate(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return DateTimeOffset.UtcNow;

            // فرمت DC date معمولاً ISO 8601 هست مثل 2025-12-27T17:59:00Z
            if (DateTimeOffset.TryParse(dateStr, out var dt))
                return dt;

            return DateTimeOffset.UtcNow;
        }
    }
}