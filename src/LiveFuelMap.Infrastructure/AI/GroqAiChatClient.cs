using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Infrastructure.AI;

public sealed class GroqAiChatClient(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ILogger<GroqAiChatClient> logger) : IAiChatClient
{
    private const string DefaultEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string DefaultModel = "llama-3.1-8b-instant";
    private readonly AiOptions _options = options.Value;

    public async Task<string> CompleteAsync(string systemPrompt, string context, string userMessage, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_options.Provider, "Groq", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported AI provider '{_options.Provider}'. Configure Ai:Provider=Groq.");

        var apiKey = _options.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Groq API key is not configured. Set GROQ_API_KEY or Ai:ApiKey.");

        var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint) ? DefaultEndpoint : _options.Endpoint.Trim();
        var model = string.IsNullOrWhiteSpace(_options.Model) ? DefaultModel : _options.Model.Trim();
        var maxTokens = Math.Clamp(_options.MaxTokens, 100, 800);
        var temperature = Math.Clamp(_options.Temperature, 0m, 1m);

        httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 90));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model,
            messages = new[]
            {
                new GroqMessage("system", TrimForPrompt(systemPrompt, 6000)),
                new GroqMessage("user", BuildUserPrompt(context, userMessage))
            },
            temperature,
            max_tokens = maxTokens
        });

        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.SendAsync(request, cancellationToken);
        stopwatch.Stop();

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("Groq rate limit reached. model={Model}; status={StatusCode}; elapsedMs={ElapsedMs}", model, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
            throw new HttpRequestException("Groq rate limit exceeded.", null, response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Groq returned non-success status. model={Model}; status={StatusCode}; elapsedMs={ElapsedMs}", model, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<GroqChatCompletionResponse>(cancellationToken: cancellationToken);
        var result = payload?.Choices?
            .Select(choice => choice.Message?.Content)
            .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content))
            ?.Trim() ?? string.Empty;

        logger.LogInformation("Groq completed. model={Model}; status={StatusCode}; elapsedMs={ElapsedMs}; responseLength={ResponseLength}", model, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, result.Length);
        return result;
    }

    private static string BuildUserPrompt(string context, string userMessage) => $"""
        Контекст LiveFuelMap, наданий backend:
        {TrimForPrompt(context, 7000)}

        Повідомлення користувача:
        {TrimForPrompt(userMessage, 3000)}
        """;

    private static string TrimForPrompt(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "...";
    }

    private sealed class GroqMessage(string role, string content)
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = role;

        [JsonPropertyName("content")]
        public string Content { get; init; } = content;
    }

    private sealed class GroqChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<GroqChoice>? Choices { get; init; }
    }

    private sealed class GroqChoice
    {
        [JsonPropertyName("message")]
        public GroqChoiceMessage? Message { get; init; }
    }

    private sealed class GroqChoiceMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
