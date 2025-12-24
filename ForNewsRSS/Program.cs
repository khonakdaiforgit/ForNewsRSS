using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ... سایر تنظیمات مثل MongoDB connection

var mongoClient = new MongoClient(builder.Configuration.GetConnectionString("MongoDB")
                                  ?? "mongodb://localhost:27017");
var mongoDatabase = mongoClient.GetDatabase("NewsDb");

// ثبت سرویس پس‌زمینه
builder.Services.AddHostedService<NewsRssBackgroundService>();
builder.Services.AddHttpClient<TelegramBotService>();

// اختیاری: برای دسترسی آسان به دیتابیس در جاهای دیگر
builder.Services.AddSingleton(mongoDatabase);

var app = builder.Build();

// ===== ایجاد ایندکس منحصربه‌فرد روی فیلد Link =====
var newsCollection = mongoDatabase.GetCollection<NewsItem>("News");

var indexKeys = Builders<NewsItem>.IndexKeys.Ascending(item => item.Link);
var indexOptions = new CreateIndexOptions { Unique = true, Name = "unique_link" };

var indexModel = new CreateIndexModel<NewsItem>(indexKeys, indexOptions);

await newsCollection.Indexes.CreateOneAsync(indexModel);

// ===================================================

app.MapGet("/", () => "News RSS Updater is running...");

app.Run();