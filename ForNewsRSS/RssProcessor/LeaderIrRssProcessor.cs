using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using ForNewsRSS.Services; 
using MongoDB.Driver;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;

namespace ForNewsRSS.RssProcessor
{
    public class LeaderIrRssProcessor : RssFeedProcessor
    {
        public LeaderIrRssProcessor(
            ILogger logger,
            IMongoDatabase database,
            TelegramBotService telegramService,
            SourceConfig config)
            : base(logger, database, telegramService, config)
        {
        }

        protected override async Task<SyndicationFeed> LoadFeedAsync(string url)
        {
            using var httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/xml, text/xml;q=0.9");
            httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.leader.ir/"); 

            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to fetch RSS: {response.StatusCode} - {errorBody}");
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var xmlReader = XmlReader.Create(stream);
            return SyndicationFeed.Load(xmlReader);
        }
    }
}