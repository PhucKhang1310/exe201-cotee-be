namespace CoTee.Configuration;





public class JwtSettings
{
    
    
    
    public string SecretKey { get; set; } = string.Empty;

    
    
    
    public string Issuer { get; set; } = string.Empty;

    
    
    
    public string Audience { get; set; } = string.Empty;

    
    
    
    public int ExpirationMinutes { get; set; } = 60;

    
    
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
            throw new InvalidOperationException("JwtSettings.SecretKey cannot be empty");

        if (SecretKey.Length < 32)
            throw new InvalidOperationException("JwtSettings.SecretKey must be at least 32 characters");

        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException("JwtSettings.Issuer cannot be empty");

        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("JwtSettings.Audience cannot be empty");

        if (ExpirationMinutes <= 0)
            throw new InvalidOperationException("JwtSettings.ExpirationMinutes must be greater than 0");
    }
}
