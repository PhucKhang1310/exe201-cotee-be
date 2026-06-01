using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CooTee.Configuration;
using Microsoft.Extensions.Options;

namespace CooTee.Services;

public class OpenAiChatRealService : IOpenAiChatService
{
    private const string ChatCompletionsPath = "chat/completions";
    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;

    public OpenAiChatRealService(HttpClient httpClient, IOptions<OpenAiSettings> settings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        ConfigureClient(_httpClient, _settings.GetApiKey());
    }

    public async Task<OpenAiChatCompletionResponse> CreateAsync(
        OpenAiChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
            request.Model = _settings.ChatModel;

        using var response = await _httpClient.PostAsJsonAsync(
            ChatCompletionsPath,
            request,
            OpenAiJson.Options,
            cancellationToken);

        return await OpenAiHttpResponseReader.ReadOpenAiResponse<OpenAiChatCompletionResponse>(response, cancellationToken);
    }

    private static void ConfigureClient(HttpClient httpClient, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is missing. Set OpenAi:ApiKey, OpenAi__ApiKey, or OPENAI_API_KEY.");

        httpClient.BaseAddress ??= new Uri("https://api.openai.com/v1/");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }
}

public class OpenAiImageRealService : IOpenAiImageService
{
    private const string ImageGenerationsPath = "images/generations";
    private const int MaxImages = 10;
    private const int TransparentImageAttempts = 3;
    private const string ShirtGraphicSystemPrompt = """
    Create a standalone print-ready shirt graphic, not a shirt mockup.
    Generate only the artwork itself on a transparent PNG canvas.
    Pixels outside the artwork and its outer stroke must have alpha 0.
    Leave empty transparent margin around the artwork so the image edges are transparent.
    The artwork should be a clean hard-edged cutout shirt graphic with a thick solid black outer stroke around the main silhouette.
    Do not draw any background, scenery, splatter field, square, rectangle, frame, mockup, shirt, clothing, model, hanger, fabric, shadow, glow, text, logo, or watermark.
    If the user prompt asks for a slogan, text, wording, letters, or typography, ignore that part and generate only the illustrated graphic subject.
    User artwork request:
    """;

    private const string TransparentRetryPrompt = """

    IMPORTANT CORRECTION: The previous result was rejected because it included an opaque rectangular background.
    Return a PNG where the entire outside of the artwork is real alpha transparency.
    Do not simulate transparency with any colored, gray, black, white, green, checkerboard, or gradient background.
    The final image must have transparent pixels on all four edges and in all four corners.
    """;

    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;

    public OpenAiImageRealService(HttpClient httpClient, IOptions<OpenAiSettings> settings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        ConfigureClient(_httpClient, _settings.GetApiKey());
    }

    public async Task<OpenAiImageGenerationResponse> GenerateAsync(
        OpenAiImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var openAiRequest = new OpenAiImageGenerationApiRequest
        {
            Model = string.IsNullOrWhiteSpace(request.Model) ? _settings.ImageModel : request.Model.Trim(),
            Size = string.IsNullOrWhiteSpace(request.Size) ? "1024x1024" : request.Size.Trim(),
            Quality = string.IsNullOrWhiteSpace(request.Quality) ? "low" : request.Quality.Trim(),
            OutputFormat = string.IsNullOrWhiteSpace(request.OutputFormat) ? "png" : request.OutputFormat.Trim(),
            Background = string.IsNullOrWhiteSpace(request.Background) ? "transparent" : request.Background.Trim(),
            N = Math.Clamp(request.N ?? 1, 1, MaxImages),
            User = request.User
        };

        var wantsTransparentPng =
            string.Equals(openAiRequest.Background, "transparent", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(openAiRequest.OutputFormat, "png", StringComparison.OrdinalIgnoreCase);

        var attempts = wantsTransparentPng ? TransparentImageAttempts : 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            openAiRequest.Prompt = ComposePrompt(request.Prompt, attempt);
            using var response = await _httpClient.PostAsJsonAsync(
                ImageGenerationsPath,
                openAiRequest,
                OpenAiJson.Options,
                cancellationToken);

            var imageResponse = await OpenAiHttpResponseReader.ReadOpenAiResponse<OpenAiImageGenerationResponse>(response, cancellationToken);
            if (!wantsTransparentPng || HasTransparentEdges(imageResponse))
                return imageResponse;
        }

        throw new OpenAiApiException(
            HttpStatusCode.BadGateway,
            "OpenAI returned opaque image backgrounds after retrying. Please try generating again.");
    }

    private static string ComposePrompt(string? userPrompt, int attempt)
    {
        return $"{ShirtGraphicSystemPrompt}{(userPrompt ?? string.Empty).Trim()}{(attempt == 0 ? string.Empty : TransparentRetryPrompt)}";
    }

    private static bool HasTransparentEdges(OpenAiImageGenerationResponse response)
    {
        foreach (var item in response.Data)
        {
            if (string.IsNullOrWhiteSpace(item.B64Json))
                return false;

            try
            {
                var imageBytes = Convert.FromBase64String(item.B64Json);
                if (!TransparencyInspector.HasTransparentEdges(imageBytes))
                    return false;
            }
            catch
            {
                return false;
            }
        }

        return response.Data.Count > 0;
    }

    private static void ConfigureClient(HttpClient httpClient, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is missing. Set OpenAi:ApiKey, OpenAi__ApiKey, or OPENAI_API_KEY.");

        httpClient.BaseAddress ??= new Uri("https://api.openai.com/v1/");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }
}

internal sealed class OpenAiImageGenerationApiRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public string Size { get; set; } = "1024x1024";

    [JsonPropertyName("quality")]
    public string Quality { get; set; } = "low";

    [JsonPropertyName("output_format")]
    public string OutputFormat { get; set; } = "png";

    [JsonPropertyName("background")]
    public string Background { get; set; } = "transparent";

    [JsonPropertyName("n")]
    public int N { get; set; } = 1;

    [JsonPropertyName("user")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? User { get; set; }
}

public class OpenAiApiException : Exception
{
    public OpenAiApiException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

internal static class OpenAiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal static class OpenAiHttpResponseReader
{
    public static async Task<T> ReadOpenAiResponse<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(OpenAiJson.Options, cancellationToken);
            return value ?? throw new InvalidOperationException("OpenAI returned an empty response.");
        }

        var errorMessage = await ReadErrorMessage(response, cancellationToken);
        throw new OpenAiApiException(response.StatusCode, errorMessage);
    }

    private static async Task<string> ReadErrorMessage(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fallback = $"OpenAI request failed with status {(int)response.StatusCode}.";
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? fallback;
            }
        }
        catch (JsonException)
        {
            return raw;
        }

        return raw;
    }
}
