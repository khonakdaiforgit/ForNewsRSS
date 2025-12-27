// File: Extensions/ReportingEndpoints.cs

using ForNewsRSS.Entities;
using MongoDB.Driver;
using System.Text;

namespace ForNewsRSS.Extensions
{
    public static class ReportingEndpoints
    {
        public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/", async (IMongoDatabase database) =>
            {
                var processLogCollection = database.GetCollection<ProcessLog>("ProcessLog");
                var errorLogCollection = database.GetCollection<TelegramErrorLog>("TelegramErrorLog");

                // دریافت لیست منحصربه‌فرد منابع
                var distinctSources = await processLogCollection
                    .Distinct(p => p.SourceName, Builders<ProcessLog>.Filter.Empty)
                    .ToListAsync();

                var sb = new StringBuilder();
                sb.AppendLine("Server Running V:1.103");
                sb.AppendLine();

                // === جدول آمار کلی منابع ===
                sb.AppendLine("=== RSS Sources Overall Statistics (All Time Summary) ===");
                sb.AppendLine();
                sb.AppendLine("+----------------+-------------+----------------+------------------------+------------------+");
                sb.AppendLine("| Source         | Executions  | Total Fetched  | Total Sent to Telegram | Total Failed     |");
                sb.AppendLine("+----------------+-------------+----------------+------------------------+------------------+");

                foreach (var source in distinctSources.OrderBy(s => s))
                {
                    var logs = await processLogCollection
                        .Find(p => p.SourceName == source)
                        .ToListAsync();

                    int executionCount = logs.Count;                 // تعداد دفعات اجرا
                    long totalFetched = logs.Sum(l => l.TotalFetched);
                    long totalSent = logs.Sum(l => l.SentToTelegram);
                    long totalFailed = logs.Sum(l => l.FailedToSend);

                    sb.AppendLine($"| {source,-14} | {executionCount,11} | {totalFetched,14} | {totalSent,22} | {totalFailed,16} |");
                }

                sb.AppendLine("+----------------+-------------+----------------+------------------------+------------------+");
                sb.AppendLine();
                sb.AppendLine();

                // === لیست آخرین ۱۰۰ خطای تلگرام ===
                sb.AppendLine("=== Last 100 Telegram Send Errors (Newest First) ===");
                sb.AppendLine();

                var recentErrors = await errorLogCollection
                    .Find(Builders<TelegramErrorLog>.Filter.Empty)
                    .SortByDescending(e => e.Timestamp)
                    .Limit(100)
                    .ToListAsync();

                if (!recentErrors.Any())
                {
                    sb.AppendLine("No errors recorded.");
                }
                else
                {
                    sb.AppendLine("+----------------+---------------------+------------------------------+------------------------------------+");
                    sb.AppendLine("| Source         | Timestamp           | News Title                   | Error Message                      |");
                    sb.AppendLine("+----------------+---------------------+------------------------------+------------------------------------+");

                    foreach (var error in recentErrors)
                    {
                        var shortTitle = error.NewsTitle.Length > 28
                            ? error.NewsTitle.Substring(0, 25) + "..."
                            : error.NewsTitle.PadRight(28);

                        var shortMessage = error.ErrorMessage.Length > 35
                            ? error.ErrorMessage.Substring(0, 32) + "..."
                            : error.ErrorMessage;

                        sb.AppendLine($"| {error.SourceName,-14} " +
                                      $"| {error.Timestamp:yyyy-MM-dd HH:mm:ss,-19} " +
                                      $"| {shortTitle,-28} " +
                                      $"| {shortMessage,-34} |");
                    }

                    sb.AppendLine("+----------------+---------------------+------------------------------+------------------------------------+");
                }

                return Results.Text(sb.ToString(), "text/plain; charset=utf-8");
            })
            .WithName("GetSystemReport");

            return app;
        }
    }
}