namespace CooTee.Configuration;





public class AppSettings
{
    
    
    
    
    public string BaseUrl { get; set; } = "http://localhost:5001";

    
    
    
    public int VerificationTokenExpirationMinutes { get; set; } = 15;

    
    
    
    public int VerificationTokenLength { get; set; } = 32;

    
    
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("AppSettings.BaseUrl cannot be empty");

        if (VerificationTokenExpirationMinutes <= 0)
            throw new InvalidOperationException("AppSettings.VerificationTokenExpirationMinutes must be greater than 0");

        if (VerificationTokenLength < 16)
            throw new InvalidOperationException("AppSettings.VerificationTokenLength must be at least 16");
    }
}
