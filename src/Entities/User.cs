using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CooTee.Entities;




[BsonIgnoreExtraElements]
public class User
{
    
    
    
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    
    
    
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    
    
    
    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    
    
    
    [BsonElement("fullName")]
    public string FullName { get; set; } = string.Empty;

    
    
    
    [BsonElement("role")]
    public string Role { get; set; } = "Customer";

    
    
    
    [BsonElement("isEmailVerified")]
    public bool IsEmailVerified { get; set; } = false;

    
    
    
    [BsonElement("verificationToken")]
    [BsonIgnoreIfNull]
    public string? VerificationToken { get; set; }

    
    
    
    [BsonElement("tokenExpiresAt")]
    [BsonIgnoreIfNull]
    public DateTime? TokenExpiresAt { get; set; }

    [BsonElement("verificationEmailLastSentAt")]
    [BsonIgnoreIfNull]
    public DateTime? VerificationEmailLastSentAt { get; set; }

    [BsonElement("passwordResetToken")]
    [BsonIgnoreIfNull]
    public string? PasswordResetToken { get; set; }

    [BsonElement("passwordResetTokenExpiresAt")]
    [BsonIgnoreIfNull]
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    
    
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    
    
    
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    
    
    
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
}
