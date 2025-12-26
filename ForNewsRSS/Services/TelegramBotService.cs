using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class TelegramBotService
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;

    public TelegramBotService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _botToken = configuration["Telegram:BotToken"]!;
    }

    public async Task SendNewsAsync(NewsItem news, string chatId)
    {
        if (!string.IsNullOrWhiteSpace(news.ImageUrl))
            await SendPhotoAsync(news, chatId);
        else
            await SendMessageAsync(news, chatId);

        await Task.Delay(1000 * 1); // 10s delay to avoid Telegram rate limits (adjust as needed)
    }

    private async Task SendMessageAsync(NewsItem news, string chatId)
    {
        var payload = new
        {
            chat_id = chatId,
            text = BuildMessage(news),
            parse_mode = "HTML"
        };

        await PostJsonAsync("sendMessage", payload);
    }

    private async Task SendPhotoAsync(NewsItem news, string chatId)
    {
        try
        {
            // ایجاد HttpClient موقت با Referer مناسب
            using var downloadClient = new HttpClient();

            var imageResponse = await downloadClient.GetAsync(news.ImageUrl);

            if (!imageResponse.IsSuccessStatusCode ||
                imageResponse.Content.Headers.ContentLength == 0)
            {
                // اگر دانلود نشد یا محتوا خالی بود → fallback به متن
                await SendMessageAsync(news, chatId);
                return;
            }

            var imageStream = await imageResponse.Content.ReadAsStreamAsync();

            // بررسی ساده اینکه آیا واقعاً تصویر است
            if (imageStream.Length == 0)
            {
                await SendMessageAsync(news, chatId);
                return;
            }

            using var form = new MultipartFormDataContent();
            var imageContent = new StreamContent(imageStream);

            // نوع تصویر را از هدر بگیریم یا پیش‌فرض jpeg بگذاریم
            var contentType = imageResponse.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            form.Add(imageContent, "photo", "news.jpg");
            form.Add(new StringContent(chatId), "chat_id");
            form.Add(new StringContent(BuildMessage(news)), "caption");
            form.Add(new StringContent("HTML"), "parse_mode");

            await PostMultipartAsync("sendPhoto", form);
        }
        catch (Exception ex)
        {
            // هر خطایی → fallback به ارسال فقط متن
            Console.WriteLine($"Failed to send photo for news: {news.Title} message:{ex.Message.ToString()}");
            await SendMessageAsync(news, chatId);
        }
    }
    private async Task PostJsonAsync(string method, object payload)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/{method}";
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Telegram API Error ({method}): {response.StatusCode} - {error}");
        }
    }

    private async Task PostMultipartAsync(string method, MultipartFormDataContent content)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/{method}";
        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Telegram API Error ({method}): {response.StatusCode} - {error}");
        }
    }

    private string BuildMessage(NewsItem news)
    {
        return $"""
        <b>{news.Title}</b>

        {news.Summary}

        📅 {news.PublishDate:yyyy-MM-dd}

        🔗 <a href="{news.Link}">Read more</a>
        """;
    }
}