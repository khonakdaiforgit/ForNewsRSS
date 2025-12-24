using System.Text;
using System.Text.Json;

public class TelegramBotService
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;
    private readonly string _chatId;

    public TelegramBotService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _botToken = configuration["Telegram:BotToken"]!;
        _chatId = configuration["Telegram:ChatId"]!;
    }

    public async Task SendNewsAsync(NewsItem news)
    {
        if (!string.IsNullOrWhiteSpace(news.ImageUrl))
            await SendPhotoAsync(news);
        else
            await SendMessageAsync(news);
    }

    private async Task SendMessageAsync(NewsItem news)
    {
        var payload = new
        {
            chat_id = _chatId,
            text = BuildMessage(news),
            parse_mode = "HTML"
        };

        await PostAsync($"sendMessage", payload);
    }

    private async Task SendPhotoAsync(NewsItem news)
    {
        var payload = new
        {
            chat_id = _chatId,
            photo = news.ImageUrl,
            caption = BuildMessage(news),
            parse_mode = "HTML"
        };

        await PostAsync($"sendPhoto", payload);
    }

    private async Task PostAsync(string method, object payload)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/{method}";
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await _httpClient.PostAsync(url, content);
    }

    private string BuildMessage(NewsItem news)
    {
        return $"""
        <b>{news.Title}</b>

        {news.Summary}

        📅 {news.PublishDate:yyyy-MM-dd}
        📰 Source: {news.Source}

        🔗 <a href="{news.Link}">Read more</a>
        """;
    }
}
