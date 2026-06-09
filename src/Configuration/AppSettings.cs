namespace CooTee.Configuration;





public class AppSettings
{
    
    
    
    
    public string BaseUrl { get; set; } = "http://localhost:5001";

    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    
    
    
    public int VerificationTokenExpirationMinutes { get; set; } = 15;

    
    
    
    public int VerificationTokenLength { get; set; } = 32;

    public int VerificationEmailResendCooldownSeconds { get; set; } = 60;

    public bool AutoVerifyEmailOnRegistration { get; set; }

    
    
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("AppSettings.BaseUrl cannot be empty");

        if (string.IsNullOrWhiteSpace(FrontendBaseUrl))
            throw new InvalidOperationException("AppSettings.FrontendBaseUrl cannot be empty");

        if (VerificationTokenExpirationMinutes <= 0)
            throw new InvalidOperationException("AppSettings.VerificationTokenExpirationMinutes must be greater than 0");

        if (VerificationTokenLength < 16)
            throw new InvalidOperationException("AppSettings.VerificationTokenLength must be at least 16");

        if (VerificationEmailResendCooldownSeconds < 0)
            throw new InvalidOperationException("AppSettings.VerificationEmailResendCooldownSeconds cannot be negative");
    }
}
