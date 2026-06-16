namespace CoTee.Configuration;

public class GoogleSettings
{
    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public void Validate()
    {
        if (!Enabled)
            return;

        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("GoogleSettings.ClientId cannot be empty when Google login is enabled");
    }
}
