using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CooTee.Entities;

[BsonIgnoreExtraElements]
public class Cart
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("items")]
    public List<CartItem> Items { get; set; } = new();
}

public class CartItem
{
    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; } = 1;

    [BsonElement("size")]
    public string Size { get; set; } = string.Empty;
}
