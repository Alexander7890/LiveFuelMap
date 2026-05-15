using System.Net.Http.Json;
using System.Text.Json;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Infrastructure.AI;

public sealed class LocalAiChatClient(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ILogger<LocalAiChatClient> logger) : IAiChatClient
{
    private readonly AiOptions _options = options.Value;

    public async Task<string> CompleteAsync(string systemPrompt, string context, string userMessage, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported local AI provider '{_options.Provider}'. Configure Ai:Provider=Ollama.");

        httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 180));

        var endpoint = BuildOllamaGenerateEndpoint(_options.Endpoint);
        var prompt = $"""
            {systemPrompt}

            Контекст backend:
            {context}

            Питання користувача:
            {userMessage}
            """;

        using var response = await httpClient.PostAsJsonAsync(endpoint, new
        {
            model = string.IsNullOrWhiteSpace(_options.Model) ? "mistral" : _options.Model,
            prompt,
            stream = false,
            options = new
            {
                temperature = 0.2,
                num_predict = 700
            }
        }, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Local AI provider returned {StatusCode}: {Body}", (int)response.StatusCode, responseBody);
            response.EnsureSuccessStatusCode();
        }

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.TryGetProperty("response", out var responseText)
            ? responseText.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string BuildOllamaGenerateEndpoint(string? endpoint)
    {
        var normalized = string.IsNullOrWhiteSpace(endpoint)
            ? "http://localhost:11434/api/generate"
            : endpoint.Trim().TrimEnd('/');

        if (normalized.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
            return normalized;

        if (normalized.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            return $"{normalized}/generate";

        return $"{normalized}/api/generate";
    }
}
