using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;

namespace ZipStation.Business.Services;

public interface IMaxToneAnalyzerService
{
    Task<(bool Success, string? Error, ToneAnalysisResult? Result)> AnalyzeAsync(string projectId, int replyCount, CancellationToken cancellationToken = default);
}

public class ToneAnalysisResult
{
    public string? ToneGuide { get; set; }
    public string? ToneAvoid { get; set; }
    public List<int> RecommendedExampleIndices { get; set; } = new();
    public List<string> Replies { get; set; } = new();
}

public class MaxToneAnalyzerService : IMaxToneAnalyzerService
{
    private const string MessagesEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int DefaultReplyCount = 25;
    private const int MaxReplyCount = 50;
    private const int MaxOutputTokens = 2000;

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    private readonly IProjectRepository _projectRepository;
    private readonly ITicketMessageRepository _ticketMessageRepository;
    private readonly ILogger<MaxToneAnalyzerService> _logger;

    public MaxToneAnalyzerService(
        IProjectRepository projectRepository,
        ITicketMessageRepository ticketMessageRepository,
        ILogger<MaxToneAnalyzerService> logger)
    {
        _projectRepository = projectRepository;
        _ticketMessageRepository = ticketMessageRepository;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error, ToneAnalysisResult? Result)> AnalyzeAsync(string projectId, int replyCount, CancellationToken cancellationToken = default)
    {
        try
        {
            var project = await _projectRepository.GetAsync(projectId);
            if (project?.Settings?.Max == null || string.IsNullOrEmpty(project.Settings.Max.ApiKeyEncrypted))
                return (false, "Max isn't configured for this project.", null);

            var apiKey = EncryptionHelper.Decrypt(project.Settings.Max.ApiKeyEncrypted);
            if (string.IsNullOrEmpty(apiKey))
                return (false, "API key couldn't be decrypted.", null);

            var model = string.IsNullOrWhiteSpace(project.Settings.Max.Model) ? "claude-sonnet-4-6" : project.Settings.Max.Model;
            var n = Math.Clamp(replyCount <= 0 ? DefaultReplyCount : replyCount, 5, MaxReplyCount);

            // Pull recent agent replies — the actual emails the maintainer sent.
            // We filter to outgoing (Source=Agent), non-internal-note messages,
            // and skip empty bodies.
            var collection = _ticketMessageRepository.GetCollection();
            var filter = Builders<TicketMessage>.Filter.Eq(m => m.ProjectId, projectId)
                       & Builders<TicketMessage>.Filter.Eq(m => m.Source, MessageSource.Agent)
                       & Builders<TicketMessage>.Filter.Eq(m => m.IsInternalNote, false)
                       & Builders<TicketMessage>.Filter.Eq(m => m.IsVoid, false);
            var messages = await collection.Find(filter)
                .SortByDescending(m => m.CreatedOnDateTime)
                .Limit(n * 2) // overshoot to allow filtering empty/short ones
                .ToListAsync(cancellationToken);

            var replies = messages
                .Select(m => StripSignatureAndQuoted(m.Body ?? string.Empty))
                .Where(b => !string.IsNullOrWhiteSpace(b) && b.Length >= 20)
                .Take(n)
                .ToList();

            if (replies.Count < 3)
                return (false, $"Need at least 3 substantial agent replies to analyze. Found {replies.Count}.", null);

            var systemPrompt = BuildSystemPrompt();
            var userMessage = BuildUserMessage(replies);

            var rawResponse = await CallAnthropicAsync(apiKey, model, systemPrompt, userMessage, cancellationToken);
            if (rawResponse == null)
                return (false, "Anthropic call failed. See server logs.", null);

            var parsed = ParseAnalysis(rawResponse);
            if (parsed == null)
                return (false, "Could not parse tone analyzer response.", null);

            parsed.Replies = replies;
            return (true, null, parsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tone analyzer failed for project {ProjectId}", projectId);
            return (false, "Unexpected error during tone analysis.", null);
        }
    }

    private static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are helping a helpdesk maintainer set up their AI assistant (Max). Max drafts replies in the maintainer's voice, so it needs a clear tone configuration.");
        sb.AppendLine();
        sb.AppendLine("You will be given 5–50 recent replies the maintainer has sent. Read them and produce three outputs:");
        sb.AppendLine("1. A `tone_guide` — positive description of how the maintainer writes.");
        sb.AppendLine("2. A `tone_avoid` list — explicit don'ts based on patterns you notice the maintainer never uses.");
        sb.AppendLine("3. A list of 3–5 reply indices you'd recommend saving as canonical examples.");
        sb.AppendLine();
        sb.AppendLine("## What to look for");
        sb.AppendLine();
        sb.AppendLine("For tone_guide describe:");
        sb.AppendLine("- Voice (first person singular vs plural, formal vs casual)");
        sb.AppendLine("- How replies open (acknowledgment pattern, salutation style)");
        sb.AppendLine("- How issues are framed (apologetic vs matter-of-fact, technical vs plain)");
        sb.AppendLine("- Information density (short and direct vs thorough)");
        sb.AppendLine("- Specific phrases or words the maintainer uses repeatedly");
        sb.AppendLine("- Sign-off style");
        sb.AppendLine();
        sb.AppendLine("For tone_avoid list patterns conspicuously absent:");
        sb.AppendLine("- Words/phrases the maintainer never uses (e.g. \"kindly\", \"I apologize for the inconvenience\")");
        sb.AppendLine("- Punctuation patterns avoided (e.g. no em-dashes, no exclamation points)");
        sb.AppendLine("- Structural patterns avoided (e.g. no boilerplate apologies)");
        sb.AppendLine();
        sb.AppendLine("For example indices pick replies that:");
        sb.AppendLine("- Demonstrate the maintainer's voice clearly");
        sb.AppendLine("- Span different ticket types (bug ack, how-to, billing, etc.)");
        sb.AppendLine("- Are reasonably self-contained");
        sb.AppendLine();
        sb.AppendLine("## Output");
        sb.AppendLine("Return JSON matching exactly:");
        sb.AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("  \"tone_guide\": \"markdown text, under 400 words\",");
        sb.AppendLine("  \"tone_avoid\": \"markdown bullet list, under 200 words\",");
        sb.AppendLine("  \"recommended_example_indices\": [0, 3, 7, 12]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Be honest. If the maintainer's tone is inconsistent, pick a dominant pattern and note it. Don't flatter or pad.");
        return sb.ToString();
    }

    private static string BuildUserMessage(List<string> replies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Here are recent replies sent by the maintainer, numbered. Each is separated by ---.");
        sb.AppendLine();
        for (int i = 0; i < replies.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
            sb.AppendLine($"[{i}]");
            sb.AppendLine(replies[i]);
        }
        return sb.ToString();
    }

    private async Task<string?> CallAnthropicAsync(string apiKey, string model, string systemPrompt, string userMessage, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);
        request.Content = JsonContent.Create(new
        {
            model,
            max_tokens = MaxOutputTokens,
            temperature = 0.3,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Anthropic returned {Status} for tone analysis: {Body}", (int)response.StatusCode, body);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("content", out var content) || content.GetArrayLength() == 0) return null;
            var first = content[0];
            if (!first.TryGetProperty("text", out var text)) return null;
            return text.GetString();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Anthropic envelope");
            return null;
        }
    }

    private static ToneAnalysisResult? ParseAnalysis(string json)
    {
        try
        {
            var trimmed = json.Trim();
            if (trimmed.StartsWith("```"))
            {
                var firstNewline = trimmed.IndexOf('\n');
                if (firstNewline > 0) trimmed = trimmed.Substring(firstNewline + 1);
                if (trimmed.EndsWith("```")) trimmed = trimmed.Substring(0, trimmed.Length - 3);
                trimmed = trimmed.Trim();
            }
            return JsonSerializer.Deserialize<ToneAnalysisResult>(trimmed, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Best-effort signature + quoted-text stripping so the analyzer focuses on
    /// the maintainer's actual words. Rough heuristics; same patterns as the
    /// SPA's reply quote splitter.
    /// </summary>
    private static string StripSignatureAndQuoted(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;
        var lines = body.Split('\n');
        int cutoff = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^On .+ wrote:\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) { cutoff = i; break; }
            if (trimmed.StartsWith("---------- Forwarded message") || trimmed.StartsWith("---------- Original Message")) { cutoff = i; break; }
            if (trimmed == "--" || trimmed == "-- ") { cutoff = i; break; }
            if (i > 0 && trimmed.StartsWith(">")) { cutoff = i; break; }
        }
        return string.Join("\n", lines.Take(cutoff)).Trim();
    }
}
