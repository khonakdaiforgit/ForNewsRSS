using System.Text;
using System.Text.Json;

public class TelegramBotService
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;
    private readonly string _chatId;
    //https://api.telegram.org/bot8001025516:AAEwKbdHszhIWxvGcWIwR-1DDLmvaGKu_xk/sendMessage?chat_id=-1003505807703&text=%D8%AA%D8%B3%D8%AA%20%D9%85%D9%88%D9%81%D9%82!%20%D8%B1%D8%A8%D8%A7%D8%AA%20%DA%A9%D8%A7%D8%B1%20%D9%85%DB%8C%E2%80%8C%DA%A9%D9%86%D9%87%20%E2%9C%85
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
