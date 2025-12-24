using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class NewsItem
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Link { get; set; } = string.Empty; 

    public string? ImageUrl { get; set; } 

    public DateTime PublishDate { get; set; }

    public string Source { get; set; } = string.Empty;
}