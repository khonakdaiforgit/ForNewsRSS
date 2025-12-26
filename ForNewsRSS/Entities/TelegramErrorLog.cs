using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ForNewsRSS.Entities
{
    public class TelegramErrorLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string SourceName { get; set; } = string.Empty;
        public string NewsLink { get; set; } = string.Empty;
        public string NewsTitle { get; set; } = string.Empty;
        public string NewsSummary { get; set; } = string.Empty;
        public string? NewsImageUrl { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Details { get; set; } = string.Empty; 
    }
}
