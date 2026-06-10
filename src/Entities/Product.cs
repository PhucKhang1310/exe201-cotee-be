using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CoTee.Entities;

[BsonIgnoreExtraElements]
public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("price")]
    public long Price { get; set; }

    [BsonElement("stock")]
    public int Stock { get; set; }

    [BsonElement("ownerId")]
    [JsonIgnore]
    public string OwnerId { get; set; } = string.Empty;
}
