using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ForNewsRSS.Entities
{
    public class PublishedNews
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string Link { get; set; } = string.Empty;
    }
}
