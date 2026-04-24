using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}")]
[Authorize]
public class KanbanStoriesController : BaseController
{
    private const int SearchLimit = 25;

    private readonly ILogger<KanbanStoriesController> _logger;
    private readonly IKanbanCardRepository _cardRepository;
    private readonly IKanbanBoardRepository _boardRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public KanbanStoriesController(
        ILogger<KanbanStoriesController> logger,
        IKanbanCardRepository cardRepository,
        IKanbanBoardRepository boardRepository,
        IProjectRepository projectRepository,
        ITicketRepository ticketRepository,
        IAppUser appUser,
        IPermissionService permissionService)
    {
        _logger = logger;
        _cardRepository = cardRepository;
        _boardRepository = boardRepository;
        _projectRepository = projectRepository;
        _ticketRepository = ticketRepository;
        _appUser = appUser;
        _permissionService = permissionService;
    }

    [HttpGet("stories")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<KanbanStorySummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        string companyId,
        [FromQuery] string? query = null,
        [FromQuery] string? projectId = null)
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
                CardNumber = c.CardNumber,
                Title = c.Title,
                Type = c.Type,
                Priority = c.Priority,
                ColumnId = c.ColumnId,
                ColumnName = columnName,
                AssignedToUserId = c.AssignedToUserId,
            };
        }).ToList();
    }
}
