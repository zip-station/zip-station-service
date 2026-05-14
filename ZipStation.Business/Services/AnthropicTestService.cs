using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ZipStation.Business.Services;

public interface IAnthropicTestService
{
    Task<(bool Success, string Message)> TestConnectionAsync(string apiKey, string model, CancellationToken cancellationToken = default);
}

public class AnthropicTestService : IAnthropicTestService
{
    private const string MessagesEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly ILogger<AnthropicTestService> _logger;

    public AnthropicTestService(ILogger<AnthropicTestService> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(string apiKey, string model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "API key is empty.");

        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);
        request.Content = JsonContent.Create(new
        {
            model,
            max_tokens = 4,
            messages = new[] { new { role = "user", content = "ping" } }
        });

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return (true, "Connection successful.");

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = ExtractErrorMessage(body);

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => (false, "Invalid API key."),
                HttpStatusCode.Forbidden => (false, "API key does not have access to this model."),
                HttpStatusCode.NotFound => (false, $"Unknown model: {model}."),
                HttpStatusCode.TooManyRequests => (false, "Rate limited by Anthropic. Try again shortly."),
                _ => (false, detail ?? $"Anthropic API returned {(int)response.StatusCode}.")
            };
        }
        catch (TaskCanceledException)
        {
            return (false, "Request to Anthropic timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error contacting Anthropic API");
            return (false, "Network error contacting Anthropic.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error testing Anthropic connection");
            return (false, "Unexpected error testing the connection.");
        }
    }

    private static string? ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }
}
