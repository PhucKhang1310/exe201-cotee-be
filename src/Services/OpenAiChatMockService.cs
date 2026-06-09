using System.Text.Json;
using System.Text.Json.Serialization;

namespace CooTee.Services;

public interface IOpenAiChatMockService
{
    OpenAiChatCompletionResponse Create(OpenAiChatCompletionRequest request);
}

public interface IOpenAiChatService
{
    Task<OpenAiChatCompletionResponse> CreateAsync(OpenAiChatCompletionRequest request, CancellationToken cancellationToken = default);
}

public class OpenAiChatMockService : IOpenAiChatMockService, IOpenAiChatService
{
    private const int MaxChoices = 10;

    public Task<OpenAiChatCompletionResponse> CreateAsync(OpenAiChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Create(request));
    }

    public OpenAiChatCompletionResponse Create(OpenAiChatCompletionRequest request)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? "gpt-4o-mini" : request.Model.Trim();
        var userText = GetLastUserText(request.Messages);
        var content = string.IsNullOrWhiteSpace(userText)
            ? "This is a mock chat completion response."
            : $"Mock response to: {userText}";
        var choiceCount = Math.Clamp(request.N ?? 1, 1, MaxChoices);

        var promptTokens = EstimatePromptTokens(request);
        var completionTokens = EstimateTextTokens(content) * choiceCount;

        return new OpenAiChatCompletionResponse
        {
            Id = $"chatcmpl-mock-{Guid.NewGuid():N}",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = model,
            Choices = CreateChoices(content, choiceCount),
            Usage = new OpenAiChatUsage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                PromptTokensDetails = new OpenAiChatPromptTokensDetails
                {
                    CachedTokens = 0,
                    AudioTokens = 0
                },
                CompletionTokensDetails = new OpenAiChatCompletionTokensDetails
                {
                    ReasoningTokens = 0,
                    AudioTokens = 0,
                    AcceptedPredictionTokens = 0,
                    RejectedPredictionTokens = 0
                }
            },
            ServiceTier = request.ServiceTier,
            SystemFingerprint = "fp_mock"
        };
    }

    private static List<OpenAiChatCompletionChoice> CreateChoices(string content, int count)
    {
        var choices = new List<OpenAiChatCompletionChoice>(count);
        for (var i = 0; i < count; i++)
        {
            choices.Add(new OpenAiChatCompletionChoice
            {
                Index = i,
                Message = new OpenAiChatMessage
                {
                    Role = "assistant",
                    Content = content
                },
                Logprobs = null,
                FinishReason = "stop"
            });
        }

        return choices;
    }

    private static string GetLastUserText(IEnumerable<OpenAiChatMessage> messages)
    {
        var lastUserMessage = messages.LastOrDefault(message =>
            string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase));

        return ExtractText(lastUserMessage?.Content);
    }

    private static int EstimatePromptTokens(OpenAiChatCompletionRequest request)
    {
        var total = EstimateTextTokens(request.Model);
        foreach (var message in request.Messages)
        {
            total += EstimateTextTokens(message.Role);
            total += EstimateTextTokens(ExtractText(message.Content));
        }

        return total;
    }

    private static string ExtractText(object? content)
    {
        if (content == null)
            return string.Empty;

        if (content is string text)
            return text;

        if (content is not JsonElement value)
            return content.ToString() ?? string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Array => ExtractTextFromContentParts(value),
            JsonValueKind.Object => value.ToString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static string ExtractTextFromContentParts(JsonElement content)
    {
        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                parts.Add(item.GetString() ?? string.Empty);
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
                continue;

            if (item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                parts.Add(text.GetString() ?? string.Empty);
            }
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static int EstimateTextTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return Math.Max(1, (int)Math.Ceiling(text.Trim().Length / 4.0));
    }
}

public class OpenAiChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OpenAiChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("n")]
    public int? N { get; set; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    [JsonPropertyName("stop")]
    public JsonElement? Stop { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    [JsonPropertyName("logit_bias")]
    public Dictionary<string, int>? LogitBias { get; set; }

    [JsonPropertyName("logprobs")]
    public bool? Logprobs { get; set; }

    [JsonPropertyName("top_logprobs")]
    public int? TopLogprobs { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("response_format")]
    public JsonElement? ResponseFormat { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("tools")]
    public JsonElement? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public JsonElement? ToolChoice { get; set; }

    [JsonPropertyName("parallel_tool_calls")]
    public bool? ParallelToolCalls { get; set; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; set; }
}

public class OpenAiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public object? Content { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }

    [JsonPropertyName("tool_calls")]
    public JsonElement? ToolCalls { get; set; }
}

public class OpenAiChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAiChatCompletionChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public OpenAiChatUsage Usage { get; set; } = new();

    [JsonPropertyName("service_tier")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceTier { get; set; }

    [JsonPropertyName("system_fingerprint")]
    public string SystemFingerprint { get; set; } = "fp_mock";
}

public class OpenAiChatCompletionChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public OpenAiChatMessage Message { get; set; } = new();

    [JsonPropertyName("logprobs")]
    public JsonElement? Logprobs { get; set; }

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = "stop";
}

public class OpenAiChatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("prompt_tokens_details")]
    public OpenAiChatPromptTokensDetails PromptTokensDetails { get; set; } = new();

    [JsonPropertyName("completion_tokens_details")]
    public OpenAiChatCompletionTokensDetails CompletionTokensDetails { get; set; } = new();
}

public class OpenAiChatPromptTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }

    [JsonPropertyName("audio_tokens")]
    public int AudioTokens { get; set; }
}

public class OpenAiChatCompletionTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }

    [JsonPropertyName("audio_tokens")]
    public int AudioTokens { get; set; }

    [JsonPropertyName("accepted_prediction_tokens")]
    public int AcceptedPredictionTokens { get; set; }

    [JsonPropertyName("rejected_prediction_tokens")]
    public int RejectedPredictionTokens { get; set; }
}
