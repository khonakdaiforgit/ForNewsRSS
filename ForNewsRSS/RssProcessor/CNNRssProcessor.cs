using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using ForNewsRSS.Services;
using MongoDB.Driver;
using System.ServiceModel.Syndication;
using System.Xml.Linq;

namespace ForNewsRSS.RssProcessor
{
    public class CNNRssProcessor : RssFeedProcessor
    {
        public CNNRssProcessor(
            ILogger logger,
            IMongoDatabase database,
            TelegramBotService telegramService,
            SourceConfig config)
            : base(logger, database, telegramService, config)
        {
        }

        protected override string? ExtractImage(SyndicationItem item)
        {
            var imageUrl = base.ExtractImage(item);
            if (!string.IsNullOrEmpty(imageUrl))
                return imageUrl;

            var ns = "http://search.yahoo.com/mrss/";

            var groupExtension = item.ElementExtensions
                .FirstOrDefault(e => e.OuterName == "group" && e.OuterNamespace == ns);

            if (groupExtension == null)
                return null;

            var groupElement = groupExtension.GetObject<XElement>();

            var contentElements = groupElement.Elements(XName.Get("content", ns))
                .Where(e => (string?)e.Attribute("medium") == "image");

            if (!contentElements.Any())
                return null;

            var bestContent = contentElements.FirstOrDefault();

            return (string?)bestContent?.Attribute("url");
        }
    }
}