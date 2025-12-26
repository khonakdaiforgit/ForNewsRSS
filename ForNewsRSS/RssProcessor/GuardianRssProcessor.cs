using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using MongoDB.Driver;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ForNewsRSS.RssProcessor
{
    public class GuardianRssProcessor : RssFeedProcessor
    {
        public GuardianRssProcessor(
            ILogger logger,
            IMongoDatabase database,
            TelegramBotService telegramService,
            SourceConfig config)
            : base(logger, database, telegramService, config)
        {
        }

        protected override NewsItem? ParseItem(SyndicationItem item, string sourceName)
        {
            var baseItem = base.ParseItem(item, sourceName);
            if (baseItem == null) return null;

            // پاک کردن HTML از Summary (strip tags)
            if (!string.IsNullOrEmpty(baseItem.Summary))
            {
                baseItem.Summary= Regex.Replace(baseItem.Summary, "<.*?>", String.Empty);
          
                // اختیاری: کوتاه کردن اگر خیلی طولانی باشه
                if (baseItem.Summary.Length > 800)
                    baseItem.Summary = baseItem.Summary.Substring(0, 800) + "...";
            }

            return baseItem;
        }

        protected override string? ExtractImage(SyndicationItem item)
        {

            // اگر پایه چیزی پیدا نکرد، دستی بگرد
            var ns = "http://search.yahoo.com/mrss/";

            var contentElements = item.ElementExtensions
                .Where(e => e.OuterName == "content" && e.OuterNamespace == ns)
                .Select(e => e.GetObject<XElement>())
                .Where(el => (string?)el.Attribute("medium") == "image" || (string?)el.Attribute("medium") == null) // گاهی medium نداره
                .Where(el => el.Attribute("url") != null);

            if (!contentElements.Any())
                return null;

            // انتخاب بهترین: بزرگ‌ترین width
            var best = contentElements
                .OrderByDescending(el =>
                {
                    return int.TryParse((string?)el.Attribute("width"), out var w) ? w : 0;
                })
                .FirstOrDefault();

            var url = (string?)best?.Attribute("url");

            // اختیاری: اگر بخوای کیفیت رو بیشتر ارتقا بدی (مثلاً width=1200 اگر موجود نبود)
            return url;
        }
    }
}