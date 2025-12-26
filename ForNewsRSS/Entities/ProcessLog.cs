using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ForNewsRSS.Entities
{
    public class ProcessLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string SourceName { get; set; } = string.Empty;
        public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;
        public int TotalFetched { get; set; } 
        public int NewInserted { get; set; } 
        public int SentToTelegram { get; set; }
        public int FailedToSend { get; set; } 
        public string Notes { get; set; } = string.Empty; 
    }
}
