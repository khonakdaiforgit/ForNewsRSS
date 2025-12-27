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
            // اول سعی کن از منطق پایه استفاده کنه (برای سازگاری با منابع دیگه)
            var imageUrl = base.ExtractImage(item);
            if (!string.IsNullOrEmpty(imageUrl))
                return imageUrl;

            // حالا برای CNN: پیدا کردن media:group
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

            // انتخاب بهترین: بزرگ‌ترین بر اساس width، یا اگر برابر بود height
            //var bestContent = contentElements
            //    .OrderByDescending(e =>
            //    {
            //        int width = int.TryParse((string?)e.Attribute("width"), out var w) ? w : 0;
            //        int height = int.TryParse((string?)e.Attribute("height"), out var h) ? h : 0;
            //        return width * height; // یا فقط width اگر افقی مهمه
            //    })
            //    .FirstOrDefault();

            // یا ساده‌تر: همیشه اولین رو بگیر (در CNN معمولاً بهترینه)
            var bestContent = contentElements.FirstOrDefault();

            return (string?)bestContent?.Attribute("url");
        }
    }
}