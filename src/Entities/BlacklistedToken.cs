using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CoTee.Entities;

[BsonIgnoreExtraElements]
public class BlacklistedToken
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("token")]
    public string Token { get; set; } = string.Empty;

    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
