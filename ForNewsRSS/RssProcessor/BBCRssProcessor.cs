using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using MongoDB.Driver;
using System.ServiceModel.Syndication;

namespace ForNewsRSS.RssProcessor
{
    public class BBCRssProcessor : RssFeedProcessor
    {
        public BBCRssProcessor(
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

            // ارتقای کیفیت تصویر BBC از 240p به 1024p
            if (!string.IsNullOrEmpty(imageUrl) && imageUrl.Contains("/240/"))
            {
                imageUrl = imageUrl.Replace("/240/", "/1024/");
            }

            return imageUrl;
        }
    }
}