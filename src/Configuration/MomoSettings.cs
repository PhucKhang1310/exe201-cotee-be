namespace CoTee.Configuration;




public class MomoSettings
{
    public string PartnerCode { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string IpnUrl { get; set; } = string.Empty;
    public string PartnerName { get; set; } = "CoTee";
    public string StoreId { get; set; } = "CoTeeStore";
    public string Language { get; set; } = "vi";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PartnerCode))
            throw new InvalidOperationException("MomoSettings.PartnerCode cannot be empty");

        if (string.IsNullOrWhiteSpace(AccessKey))
            throw new InvalidOperationException("MomoSettings.AccessKey cannot be empty");

        if (string.IsNullOrWhiteSpace(SecretKey))
            throw new InvalidOperationException("MomoSettings.SecretKey cannot be empty");

        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new InvalidOperationException("MomoSettings.Endpoint cannot be empty");

        if (string.IsNullOrWhiteSpace(RedirectUrl))
            throw new InvalidOperationException("MomoSettings.RedirectUrl cannot be empty");

        if (string.IsNullOrWhiteSpace(IpnUrl))
            throw new InvalidOperationException("MomoSettings.IpnUrl cannot be empty");
    }
}
