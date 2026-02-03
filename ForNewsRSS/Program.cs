using ForNewsRSS.Data;
using ForNewsRSS.Extensions;
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

builder.Services.AddHostedService<RssBackgroundService>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
   
}
else
{
    app.UseExceptionHandler("/Error"); 
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapReportingEndpoints();

app.MapGet("/isRunning", () => { return Results.Content("Running ..."); });

app.Run();