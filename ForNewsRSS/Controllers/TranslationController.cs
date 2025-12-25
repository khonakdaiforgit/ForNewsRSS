using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text.Json;

namespace ForNewsRSS.Controllers
{
    public class TranslationController : Controller
    {
        private readonly IMongoDatabase _database;
        private readonly TelegramBotService _telegramBotService;

        public TranslationController(IMongoDatabase database, TelegramBotService telegramBotService)
        {
            _database = database;
            _telegramBotService = telegramBotService;
        }

        // GET: /Translation یا /Translation/Index
        public async Task<IActionResult> Index(string lang = "fa")
        {
            ViewBag.SelectedLanguage = lang;
            ViewBag.LanguageName = lang == "fa" ? "فارسی" : lang.ToUpper();

            // محاسبه تعداد اخبار ترجمه‌نشده
            var count = await GetUntranslatedCountAsync(lang);
            ViewBag.UntranslatedCount = count;

            return View();
        }

        // GET: /Translation/Download?lang=fa
        public async Task<IActionResult> Download(string lang, int limit = 50)
        {
            if (string.IsNullOrEmpty(lang))
                return BadRequest("زبان مشخص نشده");

            var items = await GetUntranslatedNewsAsync(lang, limit);

            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"untranslated_{lang}.json");
        }

        // POST: /Translation/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile file, string lang)
        {
            if (file == null || file.Length == 0)
                TempData["Error"] = "فایلی انتخاب نشده است.";

            else if (string.IsNullOrEmpty(lang))
                TempData["Error"] = "زبان مشخص نشده است.";

            else
            {
                string jsonContent;
                using (var reader = new StreamReader(file.OpenReadStream()))
                    jsonContent = await reader.ReadToEndAsync();

                var translatedItems = JsonSerializer.Deserialize<List<TranslateDto>>(jsonContent);
                if (translatedItems == null)
                    TempData["Error"] = "فرمت JSON نامعتبر است.";
                else
                {
                    var savedCount = await SaveTranslationsAsync(translatedItems, lang);
                    if (savedCount > 0)
                        TempData["Success"] = $"{savedCount} خبر ترجمه‌شده با موفقیت ذخیره شد.";
                    else
                        TempData["Info"] = "هیچ خبر جدیدی برای ذخیره وجود نداشت.";
                }
            }

            return RedirectToAction(nameof(Index), new { lang });
        }

        // متدهای کمکی
        private async Task<long> GetUntranslatedCountAsync(string lang)
        {
            var original = _database.GetCollection<NewsItem>("News");
            var translated = _database.GetCollection<NewsItem>($"News_{lang}");

            var translatedLinks = await translated.Find(_ => true)
                .Project(x => x.Link)
                .ToListAsync();

            var filter = translatedLinks.Any()
                ? Builders<NewsItem>.Filter.Nin(x => x.Link, translatedLinks)
                : Builders<NewsItem>.Filter.Empty;

            return await original.CountDocumentsAsync(filter);
        }

        private async Task<List<TranslateDto>> GetUntranslatedNewsAsync(string lang, int limit)
        {
            var original = _database.GetCollection<NewsItem>("News");
            var translated = _database.GetCollection<NewsItem>($"News_{lang}");

            var translatedLinks = await translated.Find(_ => true)
                .Project(x => x.Link)
                .ToListAsync();

            var filter = translatedLinks.Any()
                ? Builders<NewsItem>.Filter.Nin(x => x.Link, translatedLinks)
                : Builders<NewsItem>.Filter.Empty;

            return await original.Find(filter)
                .SortByDescending(x => x.PublishDate)
                .Limit(limit)
                .Project(x => new TranslateDto
                {
                    Id = x.Id!,
                    Title = x.Title,
                    Summary = x.Summary
                })
                .ToListAsync();
        }
        private async Task<int> SaveTranslationsAsync(List<TranslateDto> items, string lang)
        {
            if (items == null || items.Count == 0)
                return 0;

            var original = _database.GetCollection<NewsItem>("News");
            var translatedCol = _database.GetCollection<NewsItem>($"News_{lang}");

            // 1. جمع‌آوری تمام Idهای دریافتی از JSON ترجمه‌شده
            var incomingIds = items.Select(x => x.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();

            if (!incomingIds.Any())
                return 0;

            // 2. یک کوئری واحد: تمام خبرهای اصلی با این Idها رو بگیر (به همراه Link)
            var originalNewsDict = await original.Find(x => incomingIds.Contains(x.Id!))
                .Project(x => new { x.Id, x.Link, x.ImageUrl, x.PublishDate, x.Source })
                .ToListAsync()
                .ContinueWith(t => t.Result.ToDictionary(x => x.Id, x => x));

            // 3. یک کوئری واحد: تمام Linkهای موجود در کالکشن ترجمه‌شده رو بگیر
            var existingLinks = await translatedCol.Find(_ => true)
                .Project(x => x.Link)
                .ToListAsync()
                .ContinueWith(t => new HashSet<string>(t.Result));

            // 4. حالا فقط در حافظه (RAM) پردازش کن — بدون کوئری اضافی
            var toInsert = new List<NewsItem>();

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Id) || !originalNewsDict.TryGetValue(item.Id, out var orig))
                    continue;

                if (existingLinks.Contains(orig.Link))
                    continue; // قبلاً ترجمه شده

                toInsert.Add(new NewsItem
                {
                    Title = item.Title.Trim(),
                    Summary = item.Summary.Trim(),
                    Link = orig.Link,
                    ImageUrl = orig.ImageUrl,
                    PublishDate = orig.PublishDate,
                    Source = orig.Source
                });
            }

            // 5. اگر چیزی برای درج بود، یک InsertMany انجام بده
            if (toInsert.Count > 0)
            {
                await translatedCol.InsertManyAsync(toInsert);
                // 6. ارسال در تلگرام
                SendToTelegram(toInsert);

            }


            return toInsert.Count;
        }

        private async Task SendToTelegram(List<NewsItem> toInsert)
        {
            foreach (var item in toInsert)
            {

                await _telegramBotService.SendNewsAsync(item);
                await Task.Delay(1000 * 1);
            }
        }

        // DTO برای دریافت ترجمه‌ها
        public class TranslateDto
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
        }
    }
}
