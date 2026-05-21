using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Services;

/// On-demand enrichment of an existing kanban story. Mirrors MaxEnrichmentService
/// (ticket side) but with story-specific context: no customer, no replies, no draft.
/// Triggered by the Sparkles button on the story detail page. Re-runs are idempotent
/// (soft-deletes prior pending tasks/questions before writing fresh ones).
public interface IMaxStoryEnrichmentService
{
    Task<MaxStoryEnrichment?> EnrichStoryAsync(string storyId, CancellationToken cancellationToken = default);
}

public class MaxStoryEnrichmentService : IMaxStoryEnrichmentService
{
    private const string MessagesEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxAvailableStoriesInPrompt = 25;
    private const int AvailableStoriesRecencyDays = 90;
    private const int MaxOutputTokens = 1500;

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private readonly IProjectRepository _projectRepository;
    private readonly IKanbanBoardRepository _kanbanBoardRepository;
    private readonly IKanbanCardRepository _kanbanCardRepository;
    private readonly IMaxInstructionRepository _instructionRepository;
    private readonly IMaxStoryEnrichmentRepository _enrichmentRepository;
    private readonly IMaxTaskRepository _taskRepository;
    private readonly IMaxQuestionRepository _questionRepository;
    private readonly ILogger<MaxStoryEnrichmentService> _logger;

    public MaxStoryEnrichmentService(
        IProjectRepository projectRepository,
        IKanbanBoardRepository kanbanBoardRepository,
        IKanbanCardRepository kanbanCardRepository,
        IMaxInstructionRepository instructionRepository,
        IMaxStoryEnrichmentRepository enrichmentRepository,
        IMaxTaskRepository taskRepository,
        IMaxQuestionRepository questionRepository,
        ILogger<MaxStoryEnrichmentService> logger)
    {
        _projectRepository = projectRepository;
        _kanbanBoardRepository = kanbanBoardRepository;
        _kanbanCardRepository = kanbanCardRepository;
        _instructionRepository = instructionRepository;
        _enrichmentRepository = enrichmentRepository;
        _taskRepository = taskRepository;
        _questionRepository = questionRepository;
        _logger = logger;
    }

    public async Task<MaxStoryEnrichment?> EnrichStoryAsync(string storyId, CancellationToken cancellationToken = default)
    {
        KanbanCard? story = null;
        try
        {
            story = await _kanbanCardRepository.GetAsync(storyId);
            if (story == null)
            {
                _logger.LogWarning("EnrichStory called for missing story {StoryId}", storyId);
                return null;
            }

            var project = await _projectRepository.GetAsync(story.ProjectId);
            if (project?.Settings?.Max == null || !project.Settings.Max.Enabled || string.IsNullOrEmpty(project.Settings.Max.ApiKeyEncrypted))
            {
                _logger.LogDebug("Max not enabled or no API key for project {ProjectId}; skipping story enrichment", story.ProjectId);
                return null;
            }

            var apiKey = EncryptionHelper.Decrypt(project.Settings.Max.ApiKeyEncrypted);
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Decrypted API key empty for project {ProjectId}; skipping story enrichment", story.ProjectId);
                return null;
            }

            await SetEnrichmentStatusAsync(story, "processing");

            var model = string.IsNullOrWhiteSpace(project.Settings.Max.Model) ? "claude-sonnet-4-6" : project.Settings.Max.Model;
            var instructions = await _instructionRepository.GetByProjectIdAsync(story.ProjectId);
            var board = await _kanbanBoardRepository.GetByProjectIdAsync(story.ProjectId);
            var availableStories = board != null
                ? await GetAvailableStoriesAsync(board.Id, board.ResolvedColumnId, storyId)
                : new List<KanbanCard>();

            var systemPrompt = BuildSystemPrompt(project.Settings.Max, instructions);
            var userMessage = BuildUserMessage(story, availableStories);

            var rawResponse = await CallAnthropicAsync(apiKey, model, systemPrompt, userMessage, cancellationToken);
            if (rawResponse == null)
            {
                await SetEnrichmentStatusAsync(story, "failed");
                return null;
            }

            var parsed = ParseEnrichmentResponse(rawResponse);
            if (parsed == null)
            {
                _logger.LogWarning("Failed to parse Max story enrichment response for story {StoryId}", storyId);
                await SetEnrichmentStatusAsync(story, "failed");
                return null;
            }

            // Re-enrichment: clear any prior pending tasks/questions before writing new ones.
            await _taskRepository.SoftDeletePendingByStoryIdAsync(storyId);
            await _questionRepository.SoftDeletePendingByStoryIdAsync(storyId);

            // Persist the flagged question first so the enrichment can reference its id.
            string? questionId = null;
            if (parsed.FlagQuestion && !string.IsNullOrWhiteSpace(parsed.QuestionForMaintainer))
            {
                var question = await _questionRepository.CreateAsync(new MaxQuestion
                {
                    CompanyId = story.CompanyId,
                    ProjectId = story.ProjectId,
                    SourceStoryId = storyId,
                    Question = parsed.QuestionForMaintainer!.Trim(),
                    ContextExcerpt = parsed.QuestionContextExcerpt?.Trim(),
                    Status = "pending",
                });
                questionId = question.Id;
            }

            // Resolve the duplicate target (card number → id + title) for task details.
            KanbanCard? duplicateTarget = null;
            if (parsed.SuggestedAction?.CardNumber.HasValue == true
                && string.Equals(parsed.SuggestedAction.Type, "merge_story_duplicate", StringComparison.OrdinalIgnoreCase))
            {
                duplicateTarget = await _kanbanCardRepository.GetByCardNumberAsync(story.ProjectId, parsed.SuggestedAction.CardNumber.Value);
                if (duplicateTarget?.Id == storyId) duplicateTarget = null; // guard self-merge
            }

            // Persist the enrichment record.
            var enrichment = await _enrichmentRepository.UpsertAsync(new MaxStoryEnrichment
            {
                CompanyId = story.CompanyId,
                ProjectId = story.ProjectId,
                StoryId = storyId,
                Status = "complete",
                Category = NormalizeCategory(parsed.Category),
                Summary = parsed.Summary?.Trim() ?? string.Empty,
                Confidence = Math.Clamp(parsed.Confidence, 0, 1),
                DuplicateOfStoryId = duplicateTarget?.Id,
                RelatedStoryIds = await ResolveRelatedStoryIdsAsync(story.ProjectId, parsed.RelatedCardNumbers, storyId),
                Tags = parsed.Tags ?? new(),
                SuggestedActionType = NormalizeAction(parsed.SuggestedAction?.Type),
                SuggestedNotes = parsed.SuggestedAction?.Notes?.Trim(),
                Reasoning = parsed.Reasoning?.Trim(),
                FlaggedQuestion = parsed.FlagQuestion,
                QuestionId = questionId,
                Model = model,
                RawResponse = rawResponse,
            });

            // Persist the suggested action as a pending task (when actionable).
            if (parsed.SuggestedAction is not null)
            {
                await MaybeCreateTaskAsync(story, parsed.SuggestedAction, duplicateTarget, questionId, enrichment.Confidence);
            }

            // Apply Max-discovered related stories to the card's LinkedStoryIds. Append-only;
            // never wipe maintainer-curated links. Exclude the duplicate target (that's its own
            // pending merge task, separate concern).
            var cardChanged = false;
            var duplicateTargetId = duplicateTarget?.Id;
            foreach (var relatedId in enrichment.RelatedStoryIds)
            {
                if (relatedId == story.Id) continue;
                if (relatedId == duplicateTargetId) continue;
                if (story.LinkedStoryIds.Contains(relatedId)) continue;
                story.LinkedStoryIds.Add(relatedId);
                cardChanged = true;
            }

            // Apply Max's cleaned-up description to the card if returned. Max is instructed to
            // return an empty string when no cleanup is warranted, so we only overwrite when
            // there's substantive new content.
            var cleaned = SanitizeDescriptionHtml(parsed.ImprovedDescriptionHtml);
            if (!string.IsNullOrWhiteSpace(cleaned)
                && !string.Equals(cleaned, story.DescriptionHtml, StringComparison.Ordinal))
            {
                story.DescriptionHtml = cleaned;
                cardChanged = true;
            }

            if (cardChanged)
            {
                await _kanbanCardRepository.UpdateAsync(story);
            }

            return enrichment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Story enrichment failed for {StoryId}", storyId);
            if (story != null) await SetEnrichmentStatusAsync(story, "failed");
            return null;
        }
    }

    // ---------- prompt construction ----------

    private static string BuildSystemPrompt(MaxSettings settings, List<MaxInstruction> instructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are Max, an engineering triage assistant. Look at a single kanban story and decide whether it needs attention, whether it duplicates existing work, and whether the maintainer should know something the project context doesn't already cover.");
        sb.AppendLine();
        sb.AppendLine("This story has already been created — you are NOT creating it. You are reviewing it after the fact, often after content has been edited.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(settings.ProjectContext))
        {
            sb.AppendLine("<project_context>");
            sb.AppendLine(settings.ProjectContext);
            sb.AppendLine("</project_context>");
            sb.AppendLine();
        }

        var applicable = instructions
            .Where(i => i.Contexts.Contains("enrichment") || i.Contexts.Contains("all"))
            .Take(20)
            .ToList();
        if (applicable.Count > 0)
        {
            sb.AppendLine("<instructions>");
            foreach (var inst in applicable) sb.AppendLine("- " + inst.Instruction);
            sb.AppendLine("</instructions>");
            sb.AppendLine();
        }

        sb.AppendLine("## Decision rules");
        sb.AppendLine();
        sb.AppendLine("**Category**: one of bug, feature, improvement, tech_debt, unclear.");
        sb.AppendLine();
        sb.AppendLine("**Summary**: 1 sentence, max ~140 chars. Maintainer-facing; drop pleasantries.");
        sb.AppendLine();
        sb.AppendLine("**Duplicates**: if this story duplicates a card in <available_stories>, set suggested_action.type = \"merge_story_duplicate\" and suggested_action.card_number = the target's number. Only call obvious duplicates — loosely-related stories belong in related_card_numbers instead.");
        sb.AppendLine();
        sb.AppendLine("**Related stories**: populate `related_card_numbers` with the card numbers of any stories in <available_stories> that are clearly thematically related to this one — same area of the product, overlapping symptoms, or likely-shared root cause. These become Linked Stories on the card so the maintainer can jump between them. Don't include the duplicate target (use suggested_action.card_number for that) and don't pad with weak matches.");
        sb.AppendLine();
        sb.AppendLine("**Suggested actions**: choose ONE primary action.");
        sb.AppendLine("- merge_story_duplicate: this duplicates an existing card (set suggested_action.card_number)");
        sb.AppendLine("- escalated: this looks urgent (production impact, data loss, security)");
        sb.AppendLine("- no_action: the story is clear and well-categorized; nothing to do");
        sb.AppendLine();
        sb.AppendLine("**Flag a question** when the story references something not in the project context that you'd need to understand to triage well. Skip for every minor ambiguity.");
        sb.AppendLine();
        sb.AppendLine("**Improved description**: if the current description is rambling, padded with apologies/hedging/conversational filler, or hard to scan, rewrite it cleanly in `improved_description_html`. Otherwise, return an empty string and the existing description will stay as-is. When rewriting:");
        sb.AppendLine("- Lead with what the user wants (the ask). Strip preamble, hedging, apologies.");
        sb.AppendLine("- Preserve every concrete fact verbatim: specific names, error messages, version numbers, URLs, reproduction steps, code snippets. Don't paraphrase identifiers.");
        sb.AppendLine("- Use short paragraphs. Use a `<ul>` of `<li>` bullets when the original listed several distinct points; don't fabricate bullets where the original was one continuous thought.");
        sb.AppendLine("- Allowed tags ONLY: `<p>`, `<ul>`, `<ol>`, `<li>`, `<strong>`, `<em>`, `<code>`, `<br>`. No links, images, scripts, styles, or attributes. The original source is reachable via the story's external-source link, so don't add \"original post:\" callouts.");
        sb.AppendLine("- Do NOT invent information the user didn't provide. Do NOT speculate about implementation. Do NOT add your own commentary.");
        sb.AppendLine("- If the existing description looks like maintainer-authored content (formatted with headings, lists, etc.) rather than a raw user paste, return an empty string — don't rewrite hand-curated content.");
        sb.AppendLine();
        sb.AppendLine("## Output");
        sb.AppendLine();
        sb.AppendLine("Single JSON object, no prose, no fences. Schema:");
        sb.AppendLine("{");
        sb.AppendLine("  \"category\": \"bug\" | \"feature\" | \"improvement\" | \"tech_debt\" | \"unclear\",");
        sb.AppendLine("  \"summary\": \"string\",");
        sb.AppendLine("  \"confidence\": 0..1,");
        sb.AppendLine("  \"related_card_numbers\": [number],");
        sb.AppendLine("  \"tags\": [\"string\"],");
        sb.AppendLine("  \"suggested_action\": {");
        sb.AppendLine("    \"type\": \"merge_story_duplicate\" | \"escalated\" | \"no_action\",");
        sb.AppendLine("    \"notes\": \"string\",");
        sb.AppendLine("    \"card_number\": number | null");
        sb.AppendLine("  },");
        sb.AppendLine("  \"improved_description_html\": \"string\",");
        sb.AppendLine("  \"flag_question\": boolean,");
        sb.AppendLine("  \"question_for_maintainer\": \"string\" | null,");
        sb.AppendLine("  \"question_context_excerpt\": \"string\" | null,");
        sb.AppendLine("  \"reasoning\": \"string\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string BuildUserMessage(KanbanCard story, List<KanbanCard> availableStories)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<available_stories>");
        if (availableStories.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (var s in availableStories)
            {
                sb.AppendLine($"- STR-{s.CardNumber} [{s.Type}]: {s.Title}");
            }
        }
        sb.AppendLine("</available_stories>");
        sb.AppendLine();

        sb.AppendLine("<story>");
        sb.AppendLine($"STR-{story.CardNumber}");
        sb.AppendLine($"Type: {story.Type}");
        sb.AppendLine($"Priority: {story.Priority}");
        if (story.Tags.Count > 0) sb.AppendLine($"Tags: {string.Join(", ", story.Tags)}");
        sb.AppendLine($"Title: {story.Title}");
        sb.AppendLine("Description (HTML):");
        sb.AppendLine(string.IsNullOrWhiteSpace(story.DescriptionHtml) ? "(empty)" : story.DescriptionHtml);
        sb.AppendLine("</story>");

        return sb.ToString();
    }

    // ---------- helpers ----------

    private async Task SetEnrichmentStatusAsync(KanbanCard story, string status)
    {
        // Upsert a stub record so the SPA polling sees "processing" while Anthropic chews on it.
        var existing = await _enrichmentRepository.GetByStoryIdAsync(story.Id);
        var stub = existing ?? new MaxStoryEnrichment
        {
            CompanyId = story.CompanyId,
            ProjectId = story.ProjectId,
            StoryId = story.Id,
        };
        stub.Status = status;
        await _enrichmentRepository.UpsertAsync(stub);
    }

    private async Task<List<KanbanCard>> GetAvailableStoriesAsync(string boardId, string? resolvedColumnId, string excludeStoryId)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-AvailableStoriesRecencyDays).ToUnixTimeMilliseconds();
        var filter = Builders<KanbanCard>.Filter.Eq(c => c.BoardId, boardId)
                   & Builders<KanbanCard>.Filter.Eq(c => c.IsVoid, false)
                   & Builders<KanbanCard>.Filter.Ne(c => c.Id, excludeStoryId)
                   & Builders<KanbanCard>.Filter.Gte(c => c.UpdatedOnDateTime, cutoff);
        if (!string.IsNullOrEmpty(resolvedColumnId))
            filter &= Builders<KanbanCard>.Filter.Ne(c => c.ColumnId, resolvedColumnId);

        return await _kanbanCardRepository.GetCollection()
            .Find(filter)
            .SortByDescending(c => c.UpdatedOnDateTime)
            .Limit(MaxAvailableStoriesInPrompt)
            .ToListAsync();
    }

    private async Task<List<string>> ResolveRelatedStoryIdsAsync(string projectId, List<long>? cardNumbers, string excludeStoryId)
    {
        if (cardNumbers is null || cardNumbers.Count == 0) return new List<string>();

        var ids = new List<string>();
        foreach (var n in cardNumbers.Distinct().Take(10))
        {
            var card = await _kanbanCardRepository.GetByCardNumberAsync(projectId, n);
            if (card != null && card.Id != excludeStoryId) ids.Add(card.Id);
        }
        return ids;
    }

    private async Task MaybeCreateTaskAsync(KanbanCard story, ParsedSuggestedAction action, KanbanCard? duplicateTarget, string? questionId, double confidence)
    {
        var type = NormalizeAction(action.Type);
        if (type == "no_action") return;

        var details = new MaxTaskDetails
        {
            Notes = action.Notes?.Trim(),
            QuestionId = questionId,
        };

        if (type == "merge_story_duplicate")
        {
            if (duplicateTarget == null) return; // refuse to write a merge task with no resolvable target
            details.DuplicateOfStoryId = duplicateTarget.Id;
            details.DuplicateOfStoryCardNumber = duplicateTarget.CardNumber;
            details.DuplicateOfStoryTitle = duplicateTarget.Title;
        }

        await _taskRepository.CreateAsync(new MaxTask
        {
            CompanyId = story.CompanyId,
            ProjectId = story.ProjectId,
            TicketId = string.Empty,
            StoryId = story.Id,
            Type = type,
            Status = "pending",
            Confidence = confidence,
            Details = details,
        });
    }

    private static string NormalizeCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unclear";
        return raw.Trim().ToLowerInvariant() switch
        {
            "bug" or "feature" or "improvement" or "tech_debt" or "unclear" => raw.Trim().ToLowerInvariant(),
            "techdebt" or "tech-debt" => "tech_debt",
            _ => "unclear",
        };
    }

    /// Belt-and-suspenders against either prompt-bypassing content or Max ignoring the safe-subset
    /// instructions. Strips script-y tags and inline event-handler attributes. Mirrors the worker
    /// triage service's sanitizer.
    private static readonly System.Text.RegularExpressions.Regex _scriptyTagPattern = new(
        @"<\s*/?\s*(script|style|iframe|object|embed|svg|math|link|meta|form|input|button|textarea|select|video|audio|source|track)\b[^>]*>",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex _eventHandlerAttrPattern = new(
        @"\s+on[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string SanitizeDescriptionHtml(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var trimmed = raw.Trim();
        trimmed = _scriptyTagPattern.Replace(trimmed, "");
        trimmed = _eventHandlerAttrPattern.Replace(trimmed, "");
        return trimmed;
    }

    private static string NormalizeAction(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "no_action";
        var v = raw.Trim().ToLowerInvariant();
        return v switch
        {
            "merge_story_duplicate" or "escalated" or "no_action" => v,
            // `investigate` used to be a separate action; we dropped it because the maintainer
            // had nothing to do with it. Anything Max would have flagged as investigate is now
            // either captured via related_card_numbers (becoming Linked Stories) or just lives
            // in the summary/reasoning. Map any stragglers to no_action so old prompts don't
            // create a stale pending task.
            "investigate" => "no_action",
            _ => "no_action",
        };
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
            temperature = 0,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Anthropic returned {Status} for story enrichment: {Body}", (int)response.StatusCode, body);
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
            _logger.LogWarning(ex, "Failed to parse Anthropic envelope for story enrichment");
            return null;
        }
    }

    private static ParsedEnrichment? ParseEnrichmentResponse(string json)
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

            return JsonSerializer.Deserialize<ParsedEnrichment>(trimmed, new JsonSerializerOptions
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

    private class ParsedEnrichment
    {
        public string? Category { get; set; }
        public string? Summary { get; set; }
        public double Confidence { get; set; }
        public List<long>? RelatedCardNumbers { get; set; }
        public List<string>? Tags { get; set; }
        public ParsedSuggestedAction? SuggestedAction { get; set; }
        public string? ImprovedDescriptionHtml { get; set; }
        public bool FlagQuestion { get; set; }
        public string? QuestionForMaintainer { get; set; }
        public string? QuestionContextExcerpt { get; set; }
        public string? Reasoning { get; set; }
    }

    private class ParsedSuggestedAction
    {
        public string? Type { get; set; }
        public string? Notes { get; set; }
        public long? CardNumber { get; set; }
    }
}
