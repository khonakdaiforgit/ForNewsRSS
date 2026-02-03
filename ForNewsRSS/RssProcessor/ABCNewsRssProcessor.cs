using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using ForNewsRSS.Services; 
using MongoDB.Driver;
using System.ServiceModel.Syndication;
using System.Xml.Linq;

namespace ForNewsRSS.RssProcessor
{
    public class ABCNewsRssProcessor : RssFeedProcessor
    {
        public ABCNewsRssProcessor(
            ILogger logger,
            IMongoDatabase database,
            TelegramBotService telegramService,
            SourceConfig config)
            : base(logger, database, telegramService, config)
        {
        }

        protected override string? ExtractImage(SyndicationItem item)
        {
           
            var ns = "http://search.yahoo.com/mrss/";

            var thumbnailElements = item.ElementExtensions
                .Where(e => e.OuterName == "thumbnail" && e.OuterNamespace == ns)
                .Select(e => e.GetObject<XElement>())
                .Where(el => el.Attribute("url") != null);  

            if (!thumbnailElements.Any())
                return null;

            var best = thumbnailElements
                .OrderByDescending(el =>
                {
                    int width = int.TryParse((string?)el.Attribute("width"), out var w) ? w : 0;
                    int height = int.TryParse((string?)el.Attribute("height"), out var h) ? h : 0;
                    return width * 1000 + height; 
                })
                .FirstOrDefault();

            var url = (string?)best?.Attribute("url");

            return url;
        }
    }
}