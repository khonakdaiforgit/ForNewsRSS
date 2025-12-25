using ForNewsRSS.Config;
using ForNewsRSS.Data;
using ForNewsRSS.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ========================
// تنظیمات MongoDB
// ========================
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB")
    ?? throw new InvalidOperationException("MongoDB connection string not found.");

var mongoDatabaseName = builder.Configuration["MongoDb:DatabaseName"]
    ?? "NewsDb"; // می‌تونی از appsettings جدا کنی

var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);

// ثبت به عنوان Singleton
builder.Services.AddSingleton<IMongoDatabase>(mongoDatabase);

// ========================
// ثبت سایر سرویس‌ها
// ========================

// TelegramBotService (نیاز به HttpClient داره — بهتره از IHttpClientFactory استفاده کنی)
builder.Services.AddHttpClient<TelegramBotService>(); // این خط مهمه!
builder.Services.AddScoped<TelegramBotService>(); // یا Singleton اگر مشکلی نداره

// یا اگر می‌خوای Singleton باشه (معمولاً مشکلی نداره):
// builder.Services.AddSingleton<TelegramBotService>();

// ثبت DatabaseInitializationService (یک بار در startup اجرا می‌شه)
builder.Services.AddHostedService<DatabaseInitializationService>();

// ثبت RssBackgroundService (سرویس اصلی که همه منابع رو پردازش می‌کنه)
//builder.Services.AddHostedService<RssBackgroundService>();

// ========================
// ساخت اپلیکیشن
// ========================
var app = builder.Build();

// ========================
// Middleware Pipeline
// ========================

// فقط در محیط Development این‌ها رو فعال کن
if (app.Environment.IsDevelopment())
{
    // اگر MVC یا Razor Pages داری، این‌ها رو نگه دار
    // اما اگر فقط Background Service داری، این‌ها لازم نیست
}
else
{
    app.UseExceptionHandler("/Error"); // بهتره یک صفحه خطا داشته باشی یا لاگ کنی
    app.UseHsts();
}

app.UseHttpsRedirection();

// اگر وب اپلیکیشن داری (مثل API یا صفحه وب):
// app.UseStaticFiles();
// app.UseRouting();
// app.UseAuthorization();
// app.MapControllers(); // یا MapRazorPages و ...

// اگر فقط Background Service داری (Worker Service style)، این middlewareها لازم نیست
// و حتی می‌تونی همه‌شون رو حذف کنی
app.MapGet("/", () => { return Results.Content("Running ..."); });
app.Run();