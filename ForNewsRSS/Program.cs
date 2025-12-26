using ForNewsRSS.Data;
using ForNewsRSS.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);


var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB")
    ?? throw new InvalidOperationException("MongoDB connection string not found.");

var mongoDatabaseName = builder.Configuration["MongoDb:DatabaseName"]
    ?? "NewsDb"; 

var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);

builder.Services.AddSingleton<IMongoDatabase>(mongoDatabase);


builder.Services.AddHttpClient<TelegramBotService>(); 
builder.Services.AddScoped<TelegramBotService>(); 


builder.Services.AddHostedService<DatabaseInitializationService>();

//.Services.AddHostedService<RssBackgroundService>();


var app = builder.Build();

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

app.MapGet("/", () => { return Results.Content("Running ..."); });
app.Run();