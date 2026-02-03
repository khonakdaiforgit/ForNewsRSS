using ForNewsRSS.Abstract;
using ForNewsRSS.Config;
using ForNewsRSS.Services;
using MongoDB.Driver;

namespace ForNewsRSS.RssProcessor
{
    public class _DefaultRssProcessor : RssFeedProcessor
    {
        public _DefaultRssProcessor(
            ILogger logger,
            IMongoDatabase database,
            TelegramBotService telegramService,
            SourceConfig config)
            : base(logger, database, telegramService, config)
        {
        }
    }
}