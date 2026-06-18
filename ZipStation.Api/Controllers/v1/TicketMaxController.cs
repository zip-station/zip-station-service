using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
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
[Route("api/v{version:apiVersion}/companies/{companyId}/tickets/{ticketId}/max")]
[Authorize]
public class TicketMaxController : BaseController
{
    private const double PositionStep = 1000d;

    private static readonly (string Name, string Color)[] DefaultColumns =
    {
        ("Backlog", "#94a3b8"),
        ("Ready", "#60a5fa"),
        ("Committed", "#a78bfa"),
        ("Active", "#f59e0b"),
        ("Done", "#22c55e"),
    };

    private readonly ILogger<TicketMaxController> _logger;
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketMessageRepository _ticketMessageRepository;
    private readonly ITicketGateway _ticketGateway;
    private readonly IMaxGateway _maxGateway;
    private readonly IMaxTicketEnrichmentRepository _enrichmentRepository;
    private readonly IMaxTaskRepository _taskRepository;
    private readonly IMaxQuestionRepository _questionRepository;
    private readonly IMaxEnrichmentService _enrichmentService;
    private readonly IKanbanBoardRepository _kanbanBoardRepository;
    private readonly IKanbanCardRepository _kanbanCardRepository;
    private readonly IKanbanCardNumberCounterRepository _kanbanCardNumberCounterRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditService _auditService;
    private readonly IAppUser _appUser;
    private readonly IMapper _mapper;

    public TicketMaxController(
        ILogger<TicketMaxController> logger,
        ITicketRepository ticketRepository,
        ITicketMessageRepository ticketMessageRepository,
        ITicketGateway ticketGateway,
        IMaxGateway maxGateway,
        IMaxTicketEnrichmentRepository enrichmentRepository,
        IMaxTaskRepository taskRepository,
        IMaxQuestionRepository questionRepository,
        IMaxEnrichmentService enrichmentService,
        IKanbanBoardRepository kanbanBoardRepository,
        IKanbanCardRepository kanbanCardRepository,
        IKanbanCardNumberCounterRepository kanbanCardNumberCounterRepository,
        IUserRepository userRepository,
        IAuditService auditService,
        IAppUser appUser,
        IMapper mapper)
    {
        _logger = logger;
        _ticketRepository = ticketRepository;
        _ticketMessageRepository = ticketMessageRepository;
        _ticketGateway = ticketGateway;
        _maxGateway = maxGateway;
        _enrichmentRepository = enrichmentRepository;
        _taskRepository = taskRepository;
        _questionRepository = questionRepository;
        _enrichmentService = enrichmentService;
        _kanbanBoardRepository = kanbanBoardRepository;
        _kanbanCardRepository = kanbanCardRepository;
        _kanbanCardNumberCounterRepository = kanbanCardNumberCounterRepository;
        _userRepository = userRepository;
        _auditService = auditService;
        _appUser = appUser;
        _mapper = mapper;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketMaxResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTicketMax(string companyId, string ticketId)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(ticketId);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _ticketGateway.CanGetTicketAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var enrichment = await _enrichmentRepository.GetByTicketIdAsync(ticketId);
            var tasks = await _taskRepository.GetByTicketIdAsync(ticketId);
            var allQuestions = await _questionRepository.GetByTicketIdAsync(ticketId);

            return Ok(new TicketMaxResponse
            {
                Enrichment = enrichment != null ? _mapper.Map<MaxTicketEnrichmentResponse>(enrichment) : null,
                Tasks = _mapper.Map<List<MaxTaskResponse>>(tasks),
                Questions = _mapper.Map<List<MaxQuestionResponse>>(allQuestions),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Max data for ticket {TicketId}", ticketId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("enrich")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxTicketEnrichmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReEnrich(string companyId, string ticketId)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(ticketId);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var enrichment = await _enrichmentService.EnrichTicketAsync(ticketId);
            if (enrichment == null)
                return BadRequest(new BadRequestResponse { Message = "Enrichment did not produce a result. See server logs for details." });

            return Ok(_mapper.Map<MaxTicketEnrichmentResponse>(enrichment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-enriching ticket {TicketId}", ticketId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("tasks/{taskId}/approve")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxTaskResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveTask(string companyId, string ticketId, string taskId)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(ticketId);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var task = await _taskRepository.GetAsync(taskId);
            if (task == null || task.TicketId != ticketId) return NotFound();
            if (task.Status != "pending")
                return BadRequest(new BadRequestResponse { Message = $"Task is already {task.Status}." });

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            switch (task.Type)
            {
                case "draft_reply":
                    return BadRequest(new BadRequestResponse { Message = "Send the reply from the composer; draft_reply tasks are auto-approved on send." });

                case "merge_duplicate":
                    var mergeResult = await ExecuteMergeAsync(ticket, task);
                    if (!mergeResult.ok) return BadRequest(new BadRequestResponse { Message = mergeResult.error });
                    break;

                case "add_to_backlog":
                    var backlogResult = await ExecuteAddToBacklogAsync(ticket, task, currentUser);
                    if (!backlogResult.ok) return BadRequest(new BadRequestResponse { Message = backlogResult.error });
                    break;

                case "link_to_story":
                    var linkResult = await ExecuteLinkToStoryAsync(ticket, task);
                    if (!linkResult.ok) return BadRequest(new BadRequestResponse { Message = linkResult.error });
                    break;

                case "investigate":
                case "escalated":
                    // Mark approved — no automated side effect, the maintainer is acknowledging
                    break;

                default:
                    return BadRequest(new BadRequestResponse { Message = $"Unknown task type: {task.Type}" });
            }

            task.Status = "approved";
            task.ApprovedByUserId = currentUser?.Id;
            task.ResolvedOnDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _taskRepository.UpdateAsync(task);

            await _auditService.LogAsync(companyId, ticket.ProjectId, "MaxTaskApproved", "Ticket", ticketId, _appUser, $"Type: {task.Type}");
            return Ok(_mapper.Map<MaxTaskResponse>(task));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving Max task {TaskId}", taskId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("tasks/{taskId}/reject")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxTaskResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectTask(string companyId, string ticketId, string taskId)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(ticketId);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var task = await _taskRepository.GetAsync(taskId);
            if (task == null || task.TicketId != ticketId) return NotFound();
            if (task.Status != "pending")
                return BadRequest(new BadRequestResponse { Message = $"Task is already {task.Status}." });

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            task.Status = "rejected";
            task.ApprovedByUserId = currentUser?.Id;
            task.ResolvedOnDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _taskRepository.UpdateAsync(task);

            await _auditService.LogAsync(companyId, ticket.ProjectId, "MaxTaskRejected", "Ticket", ticketId, _appUser, $"Type: {task.Type}");
            return Ok(_mapper.Map<MaxTaskResponse>(task));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting Max task {TaskId}", taskId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ----- Action executors -----

    private async Task<(bool ok, string error)> ExecuteMergeAsync(Ticket sourceTicket, MaxTask task)
    {
        var targetTicketId = task.Details.DuplicateOfTicketId;
        if (string.IsNullOrEmpty(targetTicketId))
            return (false, "Task has no target ticket id.");

        var targetTicket = await _ticketRepository.GetAsync(targetTicketId);
        if (targetTicket == null || targetTicket.CompanyId != sourceTicket.CompanyId)
            return (false, "Target ticket not found.");
        if (sourceTicket.Id == targetTicket.Id)
            return (false, "Cannot merge a ticket into itself.");

        // Refuse to merge across different customers — that would collapse two
        // separate conversations into one. Different customers reporting the
        // same issue should be linked (related_ids), not merged.
        if (!string.IsNullOrEmpty(sourceTicket.CustomerEmail)
            && !string.IsNullOrEmpty(targetTicket.CustomerEmail)
            && !string.Equals(sourceTicket.CustomerEmail, targetTicket.CustomerEmail, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Cannot merge tickets from different customers. Use Link instead so each conversation stays intact.");
        }

        var messages = await _ticketMessageRepository.GetByTicketIdAsync(sourceTicket.Id);
        foreach (var msg in messages)
        {
            msg.TicketId = targetTicket.Id;
            await _ticketMessageRepository.UpdateAsync(msg);
        }

        sourceTicket.Status = TicketStatus.Merged;
        sourceTicket.MergedIntoTicketId = targetTicket.Id;
        await _ticketRepository.UpdateAsync(sourceTicket);

        await _ticketMessageRepository.CreateAsync(new TicketMessage
        {
            TicketId = targetTicket.Id,
            CompanyId = targetTicket.CompanyId,
            ProjectId = targetTicket.ProjectId,
            Body = $"Merged from ticket #{sourceTicket.TicketNumber} ({sourceTicket.Subject}). {messages.Count} messages moved. (Approved Max suggestion.)",
            IsInternalNote = false,
            Source = MessageSource.System
        });

        return (true, "");
    }

    private async Task<(bool ok, string error)> ExecuteAddToBacklogAsync(Ticket ticket, MaxTask task, User? currentUser)
    {
        var board = await GetOrCreateBoardAsync(ticket.CompanyId, ticket.ProjectId);
        var columnId = board.ResolveIntakeColumnId();
        var cardNumber = await _kanbanCardNumberCounterRepository.GetNextCardNumberAsync(ticket.ProjectId);
        var maxPos = await _kanbanCardRepository.GetMaxPositionInColumnAsync(board.Id, columnId);

        var typeName = task.Details.SuggestedKanbanType ?? KanbanCardTypes.Improvement;
        // Max suggests by built-in name; anything unrecognized falls back to Improvement.
        var cardType = KanbanCardTypes.BuiltIns.FirstOrDefault(
            t => string.Equals(t, typeName, StringComparison.OrdinalIgnoreCase)) ?? KanbanCardTypes.Improvement;

        // Defense in depth — older pending tasks (from before the prompt fix) may still have
        // a `Kanban title:` prefix saved on disk.
        var title = StripTitlePrefix(task.Details.SuggestedTitle) ?? ticket.Subject;

        // The card's description used to default to the same string as the title, which
        // duplicated it. Now we use a brief "From ticket #N: <subject>" reference; the linked
        // ticket is available via the LinkedTickets section for full context.
        var descriptionHtml = $"<p><em>From ticket #{ticket.TicketNumber}: {System.Net.WebUtility.HtmlEncode(ticket.Subject)}</em></p>";

        var card = new KanbanCard
        {
            CompanyId = ticket.CompanyId,
            ProjectId = ticket.ProjectId,
            BoardId = board.Id,
            CardNumber = cardNumber,
            ColumnId = columnId,
            Position = maxPos + PositionStep,
            Title = title,
            DescriptionHtml = descriptionHtml,
            Type = cardType,
            Priority = TicketPriority.Normal,
            Tags = new List<string>(),
            LinkedTicketIds = new List<string> { ticket.Id },
            CreatedByUserId = currentUser?.Id,
        };
        await _kanbanCardRepository.CreateAsync(card);

        await _auditService.LogAsync(ticket.CompanyId, ticket.ProjectId, "Created", "KanbanCard", card.Id, _appUser, $"From Max add_to_backlog on ticket #{ticket.TicketNumber}");
        return (true, "");
    }

    private async Task<(bool ok, string error)> ExecuteLinkToStoryAsync(Ticket ticket, MaxTask task)
    {
        if (!task.Details.LinkToStoryCardNumber.HasValue)
            return (false, "Task is missing the target story card number.");

        var card = await _kanbanCardRepository.GetByCardNumberAsync(ticket.ProjectId, task.Details.LinkToStoryCardNumber.Value);
        if (card == null)
            return (false, "Target story not found.");

        if (card.LinkedTicketIds.Contains(ticket.Id))
            return (true, ""); // already linked, treat as success

        card.LinkedTicketIds.Add(ticket.Id);
        await _kanbanCardRepository.UpdateAsync(card);

        await _auditService.LogAsync(ticket.CompanyId, ticket.ProjectId, "LinkedTicketToStory", "KanbanCard", card.Id, _appUser, $"Linked ticket #{ticket.TicketNumber} via Max suggestion");
        return (true, "");
    }

    /// Strip label prefixes ("Kanban title:", "Title:", etc.) that Max occasionally writes into
    /// the title text. Mirrors the sanitizer in MaxEnrichmentService so older stored tasks still
    /// clean up at approval time.
    private static readonly System.Text.RegularExpressions.Regex _titlePrefixPattern = new(
        @"^\s*(kanban\s*title|card\s*title|title|card|kanban)\s*[:\-]\s*",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string? StripTitlePrefix(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var stripped = _titlePrefixPattern.Replace(raw.Trim(), "").Trim();
        return string.IsNullOrWhiteSpace(stripped) ? raw.Trim() : stripped;
    }

    private async Task<KanbanBoard> GetOrCreateBoardAsync(string companyId, string projectId)
    {
        var existing = await _kanbanBoardRepository.GetByProjectIdAsync(projectId);
        if (existing != null) return existing;

        var columns = DefaultColumns.Select(c => new KanbanColumn
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            Name = c.Name,
            Color = c.Color,
            Position = 0,
        }).ToList();
        for (int i = 0; i < columns.Count; i++) columns[i].Position = i;

        var board = new KanbanBoard
        {
            CompanyId = companyId,
            ProjectId = projectId,
            Columns = columns,
            ResolvedColumnId = columns[^1].Id,
        };
        return await _kanbanBoardRepository.CreateAsync(board);
    }
}
