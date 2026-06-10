namespace CoTee.Configuration;

public class OpenAiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string ImageModel { get; set; } = "gpt-image-1.5";
    public bool UseMock { get; set; }

    public string GetApiKey()
    {
        return FirstNonEmpty(
            ApiKey,
            Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            Environment.GetEnvironmentVariable("OpenAi__ApiKey"));
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }
}
