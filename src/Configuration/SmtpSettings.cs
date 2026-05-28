namespace CooTee.Configuration;





public class SmtpSettings
{
    
    
    
    
    public string Host { get; set; } = string.Empty;

    
    
    
    
    public int Port { get; set; } = 587;

    
    
    
    
    public string Username { get; set; } = string.Empty;

    
    
    
    
    public string Password { get; set; } = string.Empty;

    
    
    
    public string FromEmail { get; set; } = string.Empty;

    
    
    
    public string FromName { get; set; } = "CooTee Account";

    
    
    
    public bool EnableSSL { get; set; } = true;

    
    
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("SmtpSettings.Host cannot be empty");

        if (Port <= 0 || Port > 65535)
            throw new InvalidOperationException("SmtpSettings.Port must be between 1 and 65535");

        if (string.IsNullOrWhiteSpace(Username))
            throw new InvalidOperationException("SmtpSettings.Username cannot be empty");

        if (string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException("SmtpSettings.Password cannot be empty");

        if (string.IsNullOrWhiteSpace(FromEmail))
            throw new InvalidOperationException("SmtpSettings.FromEmail cannot be empty");
    }
}
