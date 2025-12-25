using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// اضافه کردن سرویس‌های MVC
builder.Services.AddControllersWithViews();

// MongoDB
var mongoClient = new MongoClient(builder.Configuration.GetConnectionString("MongoDB"));
var mongoDatabase = mongoClient.GetDatabase("NewsDb");

builder.Services.AddSingleton(mongoDatabase);

// سرویس‌های پس‌زمینه
builder.Services.AddHostedService<NewsRssBackgroundService>();

builder.Services.AddHttpClient<TelegramBotService>();

var app = builder.Build();

// Middleware های استاندارد
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// مهم: Anti-Forgery رو فعال می‌کنیم چون حالا از فرم‌های MVC استفاده می‌کنیم
app.UseAntiforgery();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ایجاد ایندکس‌ها (یک بار اجرا می‌شه)
await CreateIndexesAsync(mongoDatabase);

app.Run();

// تابع کمکی برای ایجاد ایندکس‌ها
async Task CreateIndexesAsync(IMongoDatabase db)
{
    var newsCollection = mongoDatabase.GetCollection<NewsItem>("News");
    var indexKeys = Builders<NewsItem>.IndexKeys.Ascending(item => item.Link);
    var indexOptions = new CreateIndexOptions { Unique = true, Name = "unique_link" };
    var indexModel = new CreateIndexModel<NewsItem>(indexKeys, indexOptions);
    await newsCollection.Indexes.CreateOneAsync(indexModel);

    // برای زبان‌های ترجمه‌شده
    var languages = new[] { "fa" }; // بعداً اضافه کن
    foreach (var lang in languages)
    {
        var col = db.GetCollection<NewsItem>($"News_{lang}");
        await col.Indexes.CreateOneAsync(indexModel);
    }
}