using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using MongoDB.Driver;

namespace ForNewsRSS.RssProcessor
{
    public class NYTimesRssProcessor : RssFeedProcessor
    {
        public NYTimesRssProcessor(
            ILogger logger,
            IMongoDatabase database,
            TelegramBotService telegramService,
            SourceConfig config)
            : base(logger, database, telegramService, config)
        {
            // اگر در آینده نیاز به رفتار خاص NYTimes داشتی، اینجا override کن
            // مثلاً override ExtractImage یا ParseItem
        }

        // مثال: اگر بخوای کیفیت تصویر خاصی برای NYTimes اعمال کنی
        // protected override string? ExtractImage(SyndicationItem item)
        // {
        //     var url = base.ExtractImage(item);
        //     // تغییر خاص برای NYTimes
        //     return url;
        // }
    }
}