using System.ServiceModel.Syndication;

namespace ForNewsRSS.Config
{
    public class SourceConfig
    {
        public string Name { get; set; } = string.Empty; 
        public List<string> RssUrls { get; set; } = new(); 
        public string TelegramChatId { get; set; } = string.Empty; 
        public TimeSpan FetchInterval { get; set; } = TimeSpan.FromMinutes(15);                                                
        public Func<SyndicationItem, SourceConfig, NewsItem>? CustomParser { get; set; }
    }
}
