# ForNewsRSS

A .NET-based RSS aggregator that fetches news from multiple sources (e.g., NYTimes, BBC, FoxNews, CNN, Guardian & ...), stores them in MongoDB, and forwards them to specified Telegram channels. It includes custom parsers for different RSS formats, error logging, and a simple reporting endpoint.


```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb+srv://user:pass@cluster0.....mongodb.net/?retryWrites=true&w=majority"  
},
  "RssSources": [
    {
      "Name": "NYTimes",
      "RssUrls": [ "https://rss.nytimes.com/services/xml/rss/nyt/HomePage.xml" ],
      "TelegramChatId": "-1003505807703",
      "FetchInterval": "00:05:00"
    },
    {
      "Name": "BBC",
      "RssUrls": [ "https://feeds.bbci.co.uk/news/rss.xml" ],
      "TelegramChatId": "-1003388889444",
      "FetchInterval": "00:05:00"
    },
    {
      "Name": "FoxNews",
      "RssUrls": [ "https://moxie.foxnews.com/google-publisher/latest.xml" ],
      "TelegramChatId": "-1003340534910",
      "FetchInterval": "00:05:00"
    },
    {
      "Name": "Guardian",
      "RssUrls": [ "https://www.theguardian.com/world/rss" ],
      "TelegramChatId": "-1003523020210",
      "FetchInterval": "00:05:00"
    },
    {
      "Name": "FinancialTimes",
      "RssUrls": [ "https://www.ft.com/world?format=rss" ],
      "TelegramChatId": "-1003535182556",
      "FetchInterval": "00:05:00"
    },
    {
      "Name": "NBCNews",
      "RssUrls": [ "http://feeds.nbcnews.com/feeds/worldnews" ],
      "TelegramChatId": "-1003588918503",
      "FetchInterval": "00:05:00"
    },
    {
      "Name": "ABCNews",
      "RssUrls": [ "https://abcnews.go.com/abcnews/internationalheadlines" ],
      "TelegramChatId": "-1003696143183",
      "FetchInterval": "00:05:00"
    },
    {
      "Name": "DeutscheWelle",
      "RssUrls": [ "https://rss.dw.com/rdf/rss-en-all" ],
      "TelegramChatId": "-1003682854871",
      "FetchInterval": "00:05:00"
    },
    {
      "Name": "SkyNews",
      "RssUrls": [ "https://feeds.skynews.com/feeds/rss/home.xml" ],
      "TelegramChatId": "-1003166756662",
      "FetchInterval": "00:05:00"
    }
  ],
  "Telegram": {
    "BotToken": "8001025516:AAEwKbdHszhIWxvGcWIwR-1DDLmvaGKu_xk",
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
