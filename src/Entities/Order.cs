using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CoTee.Entities;

[BsonIgnoreExtraElements]
public class Order
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("orderCode")]
    public string OrderCode { get; set; } = string.Empty;

    [BsonElement("shippingDetails")]
    public ShippingDetails ShippingDetails { get; set; } = new();

    [BsonElement("items")]
    public List<OrderItem> Items { get; set; } = new();

    [BsonElement("totalAmount")]
    public long TotalAmount { get; set; }

    [BsonElement("paymentStatus")]
    public string PaymentStatus { get; set; } = "Pending";

    [BsonElement("orderStatus")]
    public string OrderStatus { get; set; } = "Pending";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ShippingDetails
{
    [BsonElement("fullName")]
    public string FullName { get; set; } = string.Empty;

    [BsonElement("phone")]
    public string Phone { get; set; } = string.Empty;

    [BsonElement("address")]
    public string Address { get; set; } = string.Empty;
}

public class OrderItem
{
    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("imageThumbnailUrl")]
    public string? ImageThumbnailUrl { get; set; }

    [BsonElement("priceAtPurchase")]
    public long PriceAtPurchase { get; set; }

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("size")]
    public string Size { get; set; } = string.Empty;
}
