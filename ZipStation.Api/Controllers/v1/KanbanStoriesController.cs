using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}")]
[Authorize]
public class KanbanStoriesController : BaseController
{
    private const int SearchLimit = 25;
    private const int BacklogDefaultLimit = 200;
    private const int BacklogMaxLimit = 500;
    private const int BulkMaxCards = 200;

    private readonly ILogger<KanbanStoriesController> _logger;
    private readonly IKanbanCardRepository _cardRepository;
    private readonly IKanbanBoardRepository _boardRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditService _auditService;
    private readonly IMapper _mapper;
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public KanbanStoriesController(
        ILogger<KanbanStoriesController> logger,
        IKanbanCardRepository cardRepository,
        IKanbanBoardRepository boardRepository,
        IProjectRepository projectRepository,
        ITicketRepository ticketRepository,
        IUserRepository userRepository,
        IAuditService auditService,
        IMapper mapper,
        IAppUser appUser,
        IPermissionService permissionService)
    {
        _logger = logger;
        _cardRepository = cardRepository;
        _boardRepository = boardRepository;
        _projectRepository = projectRepository;
        _ticketRepository = ticketRepository;
        _userRepository = userRepository;
        _auditService = auditService;
        _mapper = mapper;
        _appUser = appUser;
        _permissionService = permissionService;
    }

    [HttpGet("stories")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<KanbanStorySummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        string companyId,
        [FromQuery] string? query = null,
        [FromQuery] string? projectId = null,
        [FromQuery] string? status = null)
    {
        try
        {
            if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId)) return Unauthorized();

            var accessibleProjectIds = await _permissionService.GetAccessibleProjectIdsAsync(_appUser.UserId, companyId);
            if (accessibleProjectIds.Count == 0) return Ok(new List<KanbanStorySummaryResponse>());

            var collection = _cardRepository.GetCollection();
            var filter = Builders<KanbanCard>.Filter.Eq(c => c.CompanyId, companyId)
                       & Builders<KanbanCard>.Filter.Eq(c => c.IsVoid, false)
                       & Builders<KanbanCard>.Filter.In(c => c.ProjectId, accessibleProjectIds);

            if (!string.IsNullOrEmpty(projectId))
                filter &= Builders<KanbanCard>.Filter.Eq(c => c.ProjectId, projectId);

            // Filter by state (kanban column name, e.g. "To Do", "In Progress", "Done").
            // Columns are per-board, so resolve the matching column IDs across the projects in scope.
            if (!string.IsNullOrWhiteSpace(status))
            {
                var scopedProjectIds = !string.IsNullOrEmpty(projectId)
                    ? new List<string> { projectId }
                    : accessibleProjectIds;

                var matchingColumnIds = new List<string>();
                foreach (var pid in scopedProjectIds)
                {
                    var board = await _boardRepository.GetByProjectIdAsync(pid);
                    if (board == null) continue;
                    matchingColumnIds.AddRange(board.Columns
                        .Where(col => string.Equals(col.Name, status, StringComparison.OrdinalIgnoreCase))
                        .Select(col => col.Id));
                }

                // No column matches the requested state → no stories can be in it.
                if (matchingColumnIds.Count == 0) return Ok(new List<KanbanStorySummaryResponse>());

                filter &= Builders<KanbanCard>.Filter.In(c => c.ColumnId, matchingColumnIds);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var trimmed = query.Trim().TrimStart('#');
                var regex = new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(trimmed), "i");
                var orFilters = new List<FilterDefinition<KanbanCard>>
                {
                    Builders<KanbanCard>.Filter.Regex(c => c.Title, regex),
                };

                // Match STR-NN or NN
                var numberPart = trimmed.StartsWith("STR-", StringComparison.OrdinalIgnoreCase) ? trimmed[4..] : trimmed;
                if (long.TryParse(numberPart.TrimStart('0'), out var num) && num > 0)
                    orFilters.Add(Builders<KanbanCard>.Filter.Eq(c => c.CardNumber, num));

                filter &= Builders<KanbanCard>.Filter.Or(orFilters);
            }

            var cards = await collection.Find(filter)
                .SortByDescending(c => c.UpdatedOnDateTime)
                .Limit(SearchLimit)
                .ToListAsync();

            return Ok(await BuildSummariesAsync(cards));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching kanban stories for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    /// Cross-project backlog grid. Returns full backlog rows across every project the caller can
    /// access (optionally narrowed to specific projects/boards), filtered by lifecycle status
    /// (default Backlog + Committed), type, priority, tags, assignee and free text, with sorting
    /// and pagination. This is the data source for the new backlog grid in the SPA.
    [HttpGet("stories/backlog")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<KanbanStorySummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Backlog(
        string companyId,
        [FromQuery] string? query = null,
        [FromQuery] List<string>? projectIds = null,
        [FromQuery] List<string>? boardIds = null,
        [FromQuery] List<string>? status = null,
        [FromQuery] string? type = null,
        [FromQuery] TicketPriority? priority = null,
        [FromQuery] List<string>? tags = null,
        [FromQuery] string? assignedTo = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? dir = null,
        [FromQuery] int limit = BacklogDefaultLimit,
        [FromQuery] int skip = 0)
    {
        try
        {
            if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId)) return Unauthorized();

            var accessibleProjectIds = await _permissionService.GetAccessibleProjectIdsAsync(_appUser.UserId, companyId);
            if (accessibleProjectIds.Count == 0) return Ok(new List<KanbanStorySummaryResponse>());

            // Narrow to the requested projects, but never beyond what the caller can access.
            var scopedProjectIds = projectIds != null && projectIds.Count > 0
                ? accessibleProjectIds.Intersect(projectIds).ToList()
                : accessibleProjectIds;
            if (scopedProjectIds.Count == 0) return Ok(new List<KanbanStorySummaryResponse>());

            var statuses = KanbanStatusRules.ParseStatusFilter(status);

            var f = Builders<KanbanCard>.Filter;
            var filter = f.Eq(c => c.CompanyId, companyId)
                       & f.Eq(c => c.IsVoid, false)
                       & f.In(c => c.ProjectId, scopedProjectIds)
                       & f.In(c => c.Status, statuses);

            if (boardIds != null && boardIds.Count > 0)
                filter &= f.In(c => c.BoardId, boardIds);

            if (!string.IsNullOrWhiteSpace(type))
                filter &= f.Eq(c => c.Type, type);

            if (priority.HasValue)
                filter &= f.Eq(c => c.Priority, priority.Value);

            if (tags != null && tags.Count > 0)
                filter &= f.AnyIn(c => c.Tags, tags);

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                if (assignedTo == "unassigned")
                    filter &= f.Eq(c => c.AssignedToUserId, (string?)null);
                else
                {
                    var assignee = assignedTo;
                    if (assignedTo == "me")
                        assignee = (await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId))?.Id;
                    filter &= f.Eq(c => c.AssignedToUserId, assignee);
                }
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var trimmed = query.Trim().TrimStart('#');
                var regex = new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(trimmed), "i");
                var orFilters = new List<FilterDefinition<KanbanCard>>
                {
                    f.Regex(c => c.Title, regex),
                    f.Regex(c => c.DescriptionHtml, regex),
                };
                var numberPart = trimmed.StartsWith("STR-", StringComparison.OrdinalIgnoreCase) ? trimmed[4..] : trimmed;
                if (long.TryParse(numberPart.TrimStart('0'), out var num) && num > 0)
                    orFilters.Add(f.Eq(c => c.CardNumber, num));
                filter &= f.Or(orFilters);
            }

            var sortDef = BuildBacklogSort(sort, dir);
            var take = Math.Clamp(limit, 1, BacklogMaxLimit);
            var cards = await _cardRepository.GetCollection()
                .Find(filter)
                .Sort(sortDef)
                .Skip(Math.Max(skip, 0))
                .Limit(take)
                .ToListAsync();

            return Ok(await BuildSummariesAsync(cards));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading backlog for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    /// Bulk edit selected stories — change type/priority/assignee/tags or transition lifecycle
    /// status (commit / obsolete / archive / send to backlog / mark reviewed) across many stories
    /// at once. A selection may span projects; each card is gated by Kanban.Edit on its OWN project,
    /// and cards the caller can't edit (or that don't exist) are skipped and reported back.
    [HttpPost("stories/bulk")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(BulkUpdateStoriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Bulk(string companyId, [FromBody] BulkUpdateStoriesRequest request)
    {
        try
        {
            if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId)) return Unauthorized();

            if (request?.CardIds == null || request.CardIds.Count == 0)
                return BadRequest(new BadRequestResponse { Message = "No stories selected" });
            if (request.CardIds.Count > BulkMaxCards)
                return BadRequest(new BadRequestResponse { Message = $"Too many stories selected (max {BulkMaxCards})" });

            var ids = request.CardIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var cards = await _cardRepository.GetCollection()
                .Find(Builders<KanbanCard>.Filter.In(c => c.Id, ids)
                    & Builders<KanbanCard>.Filter.Eq(c => c.CompanyId, companyId)
                    & Builders<KanbanCard>.Filter.Eq(c => c.IsVoid, false))
                .ToListAsync();

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var canEditByProject = new Dictionary<string, bool>();
            var boardsById = new Dictionary<string, KanbanBoard>();
            var updated = new List<KanbanCard>();
            var skipped = new List<string>();

            foreach (var card in cards)
            {
                if (!canEditByProject.TryGetValue(card.ProjectId, out var canEdit))
                {
                    canEdit = await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.KanbanEdit, card.ProjectId);
                    canEditByProject[card.ProjectId] = canEdit;
                }
                if (!canEdit) { skipped.Add(card.Id); continue; }

                if (!boardsById.TryGetValue(card.BoardId, out var board))
                {
                    var loaded = await _boardRepository.GetAsync(card.BoardId);
                    if (loaded == null) { skipped.Add(card.Id); continue; }
                    board = loaded;
                    boardsById[card.BoardId] = board;
                }

                if (request.Priority.HasValue) card.Priority = request.Priority.Value;
                if (request.Tags != null) card.Tags = request.Tags;
                if (request.ClearAssignee == true) card.AssignedToUserId = null;
                else if (request.AssignedToUserId != null) card.AssignedToUserId = request.AssignedToUserId;

                if (!string.IsNullOrWhiteSpace(request.Type)
                    && (KanbanCardTypes.IsBuiltIn(request.Type) || board.CustomCardTypes.Any(t => t.Id == request.Type)))
                    card.Type = request.Type;

                if (request.Status.HasValue && request.Status.Value != card.Status)
                {
                    var plan = KanbanStatusRules.PlanStatusChange(card, request.Status.Value, board, now);
                    card.Status = plan.Status;
                    if (plan.MoveToColumnId != null) card.ColumnId = plan.MoveToColumnId;
                    if (plan.PlaceAtBoardEntry)
                    {
                        var maxPos = await _cardRepository.GetMaxPositionInColumnAsync(board.Id, card.ColumnId);
                        card.Position = maxPos + 1000d;
                    }
                    if (plan.ResolvedChanged) card.ResolvedOnDateTime = plan.ResolvedOnDateTime;
                }

                card.UpdatedByUserId = (await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId))?.Id;
                var saved = await _cardRepository.UpdateAsync(card);
                await _auditService.LogAsync(companyId, card.ProjectId, "Updated", "KanbanCard", saved.Id, _appUser, $"Bulk edit STR-{saved.CardNumber}");
                updated.Add(saved);
            }

            return Ok(new BulkUpdateStoriesResponse
            {
                UpdatedCount = updated.Count,
                SkippedCardIds = skipped,
                Updated = await BuildSummariesAsync(updated),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk-updating stories for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    private static SortDefinition<KanbanCard> BuildBacklogSort(string? sort, string? dir)
    {
        var s = Builders<KanbanCard>.Sort;
        var descending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        switch ((sort ?? "backlog").ToLowerInvariant())
        {
            case "priority":
                // Highest priority first by default (Urgent=3 → Low=0).
                return descending
                    ? s.Ascending(c => c.Priority).Descending(c => c.BacklogPosition)
                    : s.Descending(c => c.Priority).Ascending(c => c.BacklogPosition);
            case "updated":
                return descending ? s.Ascending(c => c.UpdatedOnDateTime) : s.Descending(c => c.UpdatedOnDateTime);
            case "created":
                return descending ? s.Ascending(c => c.CreatedOnDateTime) : s.Descending(c => c.CreatedOnDateTime);
            case "number":
                return descending ? s.Descending(c => c.CardNumber) : s.Ascending(c => c.CardNumber);
            case "title":
                return descending ? s.Descending(c => c.Title) : s.Ascending(c => c.Title);
            // Hand-prioritized backlog order (drag-to-prioritize): lower BacklogPosition = top.
            default:
                return descending
                    ? s.Descending(c => c.BacklogPosition).Descending(c => c.CreatedOnDateTime)
                    : s.Ascending(c => c.BacklogPosition).Ascending(c => c.CreatedOnDateTime);
        }
    }

    [HttpGet("tickets/{ticketId}/linked-stories")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<KanbanStorySummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLinkedStories(string companyId, string ticketId)
    {
        try
        {
            if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId)) return Unauthorized();

            var ticket = await _ticketRepository.GetAsync(ticketId);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.TicketsView, ticket.ProjectId))
                return Unauthorized(new BadRequestResponse { Message = "Insufficient permissions" });

            var cards = await _cardRepository.GetByTicketIdAsync(ticketId);
            // Filter out cards in projects the user can't access
            var accessibleProjectIds = (await _permissionService.GetAccessibleProjectIdsAsync(_appUser.UserId, companyId)).ToHashSet();
            cards = cards.Where(c => accessibleProjectIds.Contains(c.ProjectId)).ToList();

            return Ok(await BuildSummariesAsync(cards));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading linked stories for ticket {TicketId}", ticketId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    private async Task<List<KanbanStorySummaryResponse>> BuildSummariesAsync(List<KanbanCard> cards)
    {
        if (cards.Count == 0) return new List<KanbanStorySummaryResponse>();

        var projectIds = cards.Select(c => c.ProjectId).Distinct().ToList();
        var projectsById = new Dictionary<string, Project>();
        foreach (var pid in projectIds)
        {
            var p = await _projectRepository.GetAsync(pid);
            if (p != null) projectsById[pid] = p;
        }

        var boardIds = cards.Select(c => c.BoardId).Distinct().ToList();
        var boardsById = new Dictionary<string, KanbanBoard>();
        foreach (var bid in boardIds)
        {
            var b = await _boardRepository.GetAsync(bid);
            if (b != null) boardsById[bid] = b;
        }

        return cards.Select(c =>
        {
            projectsById.TryGetValue(c.ProjectId, out var project);
            boardsById.TryGetValue(c.BoardId, out var board);
            var columnName = board?.Columns.FirstOrDefault(col => col.Id == c.ColumnId)?.Name;
            return new KanbanStorySummaryResponse
            {
                Id = c.Id,
                ProjectId = c.ProjectId,
                ProjectName = project?.Name,
                BoardId = c.BoardId,
                CardNumber = c.CardNumber,
                Title = c.Title,
                Type = c.Type,
                Priority = c.Priority,
                ColumnId = c.ColumnId,
                ColumnName = columnName,
                IsResolved = c.Status is KanbanStoryStatus.Resolved,
                AssignedToUserId = c.AssignedToUserId,
                Status = c.Status,
                BacklogPosition = c.BacklogPosition,
                Tags = c.Tags,
                ExternalSources = _mapper.Map<List<KanbanCardExternalSourceResponse>>(c.ExternalSources),
                UpdatedOnDateTime = c.UpdatedOnDateTime,
                CreatedOnDateTime = c.CreatedOnDateTime,
            };
        }).ToList();
    }
}

public class BulkUpdateStoriesRequest
{
    public List<string> CardIds { get; set; } = new();

    /// Lifecycle transition to apply to every selected story (optional).
    public KanbanStoryStatus? Status { get; set; }

    /// Built-in type name or a custom type id. Applied only to cards whose board defines it.
    public string? Type { get; set; }

    public TicketPriority? Priority { get; set; }

    public string? AssignedToUserId { get; set; }

    public bool? ClearAssignee { get; set; }

    /// Replaces the tag list on every selected story when provided.
    public List<string>? Tags { get; set; }
}

public class BulkUpdateStoriesResponse
{
    public int UpdatedCount { get; set; }
    public List<string> SkippedCardIds { get; set; } = new();
    public List<KanbanStorySummaryResponse> Updated { get; set; } = new();
}
