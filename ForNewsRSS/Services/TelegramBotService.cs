// File: TelegramBotService.cs
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ForNewsRSS.Services
{
    public class TelegramBotService
    {
        private readonly HttpClient _httpClient;
        private readonly string _botToken;
        private readonly ILogger<TelegramBotService> _logger;

        public TelegramBotService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<TelegramBotService> logger)
        {
            _httpClient = httpClient;
            _botToken = configuration["Telegram:BotToken"]
                ?? throw new InvalidOperationException("Telegram:BotToken is missing in configuration.");
            _logger = logger;
        }

        public async Task SendNewsAsync(NewsItem news, string chatId)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(news.ImageUrl))
                {
                    await SendPhotoWithUrlAsync(news, chatId);
                }
                else
                {
                    await SendMessageAsync(news, chatId);
                }

                // تأخیر ۱ ثانیه برای جلوگیری از rate limit تلگرام (حدود ۳۰ پیام در ثانیه به یک چت، اما بهتر محافظه‌کار باشیم)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending news to Telegram (ChatId: {ChatId}, Title: {Title})",
                    chatId, news.Title);
            }
        }

        private async Task SendMessageAsync(NewsItem news, string chatId)
        {
            var payload = new
            {
                chat_id = chatId,
                text = BuildMessage(news),
                parse_mode = "HTML",
                disable_web_page_preview = false
            };

            await PostJsonAsync("sendMessage", payload);
            _logger.LogInformation("Message sent to chat {ChatId}: {Title}", chatId, news.Title);
        }

        private async Task SendPhotoWithUrlAsync(NewsItem news, string chatId)
        {
            var payload = new
            {
                chat_id = chatId,
                photo = news.ImageUrl,                    // مستقیم URL ارسال می‌شود
                caption = BuildMessage(news),
                parse_mode = "HTML",
                disable_web_page_preview = true           // برای جلوگیری از پیش‌نمایش لینک در کپشن
            };

            try
            {
                await PostJsonAsync("sendPhoto", payload);
                _logger.LogInformation("Photo sent via URL to chat {ChatId}: {Title}", chatId, news.Title);
            }
            catch (Exception ex) when (ex.Message.Contains("400") || ex.Message.Contains("BAD_REQUEST"))
            {
                // اگر URL تصویر مشکل داشت (مثلاً تلگرام نتونست دانلود کنه)، fallback به متن
                _logger.LogWarning(ex, "Failed to send photo via URL (fallback to text): {Title} - ImageUrl: {ImageUrl}",
                    news.Title, news.ImageUrl);

                await SendMessageAsync(news, chatId);
            }
        }

        private async Task PostJsonAsync(string method, object payload)
        {
            var url = $"https://api.telegram.org/bot{_botToken}/{method}";
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();

                // تشخیص خطای Too Many Requests
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (System.Text.Json.JsonDocument.Parse(errorBody).RootElement.TryGetProperty("parameters", out var paramsElement) &&
                        paramsElement.TryGetProperty("retry_after", out var retryElement) &&
                        retryElement.TryGetInt32(out var retryAfter))
                    {
                        _logger.LogWarning("Telegram rate limit hit. Retrying after {RetryAfter} seconds.", retryAfter);
                        await Task.Delay(TimeSpan.FromSeconds(retryAfter + 1)); // +1 برای احتیاط
                                                                                // دوباره امتحان کن (recursive یا loop)
                        await PostJsonAsync(method, payload); // retry
                        return;
                    }
                }

                var errorMessage = $"Telegram API Error ({method}): {response.StatusCode} - {errorBody}";
                _logger.LogError("Telegram API call failed: {ErrorMessage}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
        }

        private string BuildMessage(NewsItem news)
        {
            // استفاده از raw string literal برای خوانایی بیشتر
            return $"""
                    <b>{EscapeHtml(news.Title)}</b>

                    {EscapeHtml(news.Summary)}

                    📅 {news.PublishDate:yyyy-MM-dd}

                    🔗 <a href="{news.Link}">Read more</a>
                    """;
        }

        // جلوگیری از مشکلات HTML injection در عنوان یا خلاصه (اختیاری اما توصیه می‌شود)
        private static string EscapeHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}