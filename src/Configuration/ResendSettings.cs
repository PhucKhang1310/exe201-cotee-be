namespace CooTee.Configuration;

public class ResendSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.resend.com";
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "CooTee Account";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("ResendSettings.ApiKey cannot be empty");

        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("ResendSettings.ApiBaseUrl must be an absolute URL");

        if (string.IsNullOrWhiteSpace(FromEmail))
            throw new InvalidOperationException("ResendSettings.FromEmail cannot be empty");
    }
}
