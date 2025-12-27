using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using ForNewsRSS.Services;  // اگر لازم باشه، اما در کدت نیست
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
           
            // حالا دستی بگرد برای ABC News
            var ns = "http://search.yahoo.com/mrss/";

            var thumbnailElements = item.ElementExtensions
                .Where(e => e.OuterName == "thumbnail" && e.OuterNamespace == ns)
                .Select(e => e.GetObject<XElement>())
                .Where(el => el.Attribute("url") != null);  // فقط اونایی که URL دارن (medium رو حذف کردم چون همیشه null هست)

            if (!thumbnailElements.Any())
                return null;

            // انتخاب بهترین: بزرگ‌ترین width (اگر width برابر بود، height رو چک کن)
            var best = thumbnailElements
                .OrderByDescending(el =>
                {
                    int width = int.TryParse((string?)el.Attribute("width"), out var w) ? w : 0;
                    int height = int.TryParse((string?)el.Attribute("height"), out var h) ? h : 0;
                    return width * 1000 + height;  // برای اولویت width، بعد height
                })
                .FirstOrDefault();

            var url = (string?)best?.Attribute("url");

            // اختیاری: اگر بخوای کیفیت رو ارتقا بدی (مثل جایگزین کردن اندازه)
            // مثلاً اگر URL شامل "_16x9_992" باشه، به "_16x9_1600" تغییر بده اگر موجود باشه
            // اما در ABC News معمولاً لازم نیست، چون بزرگ‌ترین رو انتخاب کردی

            return url;
        }
    }
}