using System.ServiceModel.Syndication;

namespace ForNewsRSS.Config
{
    public class SourceConfig
    {
        public string Name { get; set; } = string.Empty; // e.g., "NYTimes"
        public List<string> RssUrls { get; set; } = new(); // List of RSS feed URLs
        public string TelegramChatId { get; set; } = string.Empty; // Per-source channel
        public TimeSpan FetchInterval { get; set; } = TimeSpan.FromMinutes(15); // Customizable per source
                                                                                // Optional: Custom parser delegate if needed (for extreme variations)
        public Func<SyndicationItem, SourceConfig, NewsItem>? CustomParser { get; set; }
    }
}
