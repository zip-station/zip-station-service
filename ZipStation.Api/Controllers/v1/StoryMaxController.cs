using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/projects/{projectId}/board/cards/{cardId}/max")]
[Authorize]
public class StoryMaxController : BaseController
{
    private readonly ILogger<StoryMaxController> _logger;
    private readonly IKanbanCardRepository _cardRepository;
    private readonly IKanbanCardCommentRepository _commentRepository;
    private readonly IKanbanBoardGateway _kanbanGateway;
    private readonly IMaxGateway _maxGateway;
    private readonly IMaxStoryEnrichmentRepository _enrichmentRepository;
    private readonly IMaxTaskRepository _taskRepository;
    private readonly IMaxQuestionRepository _questionRepository;
    private readonly IMaxStoryEnrichmentService _enrichmentService;
    private readonly IUserRepository _userRepository;
    private readonly IAuditService _auditService;
    private readonly IAppUser _appUser;
    private readonly IMapper _mapper;

    public StoryMaxController(
        ILogger<StoryMaxController> logger,
        IKanbanCardRepository cardRepository,
        IKanbanCardCommentRepository commentRepository,
        IKanbanBoardGateway kanbanGateway,
        IMaxGateway maxGateway,
        IMaxStoryEnrichmentRepository enrichmentRepository,
        IMaxTaskRepository taskRepository,
        IMaxQuestionRepository questionRepository,
        IMaxStoryEnrichmentService enrichmentService,
        IUserRepository userRepository,
        IAuditService auditService,
        IAppUser appUser,
        IMapper mapper)
    {
        _logger = logger;
        _cardRepository = cardRepository;
        _commentRepository = commentRepository;
        _kanbanGateway = kanbanGateway;
        _maxGateway = maxGateway;
        _enrichmentRepository = enrichmentRepository;
        _taskRepository = taskRepository;
        _questionRepository = questionRepository;
        _enrichmentService = enrichmentService;
        _userRepository = userRepository;
        _auditService = auditService;
        _appUser = appUser;
        _mapper = mapper;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(StoryMaxResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStoryMax(string companyId, string projectId, string cardId)
    {
        try
        {
            var card = await _cardRepository.GetAsync(cardId);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _kanbanGateway.CanViewAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var enrichment = await _enrichmentRepository.GetByStoryIdAsync(cardId);
            var tasks = await _taskRepository.GetByStoryIdAsync(cardId);
            var questions = await _questionRepository.GetByStoryIdAsync(cardId);

            return Ok(new StoryMaxResponse
            {
                Enrichment = enrichment != null ? _mapper.Map<MaxStoryEnrichmentResponse>(enrichment) : null,
                Tasks = _mapper.Map<List<MaxTaskResponse>>(tasks),
                Questions = _mapper.Map<List<MaxQuestionResponse>>(questions),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Max data for story {CardId}", cardId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("enrich")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxStoryEnrichmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReEnrich(string companyId, string projectId, string cardId)
    {
        try
        {
            var card = await _cardRepository.GetAsync(cardId);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var enrichment = await _enrichmentService.EnrichStoryAsync(cardId);
            if (enrichment == null)
                return BadRequest(new BadRequestResponse { Message = "Enrichment did not produce a result. See server logs for details." });

            return Ok(_mapper.Map<MaxStoryEnrichmentResponse>(enrichment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-enriching story {CardId}", cardId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("tasks/{taskId}/approve")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxTaskResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveTask(string companyId, string projectId, string cardId, string taskId)
    {
        try
        {
            var card = await _cardRepository.GetAsync(cardId);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            var task = await _taskRepository.GetAsync(taskId);
            if (task == null || task.StoryId != cardId) return NotFound();
            if (task.Status != "pending")
                return BadRequest(new BadRequestResponse { Message = $"Task is already {task.Status}." });

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            switch (task.Type)
            {
                case "merge_story_duplicate":
                    var mergeResult = await ExecuteStoryMergeAsync(card, task, currentUser);
                    if (!mergeResult.ok) return BadRequest(new BadRequestResponse { Message = mergeResult.error });
                    break;

                case "escalated":
                    // Maintainer acknowledged — no automated side effect.
                    break;

                case "investigate":
                    // `investigate` was retired. Any stragglers from old prompts are no-op accepted
                    // so the dashboard can clear them; new enrichment never produces this type.
                    break;

                default:
                    return BadRequest(new BadRequestResponse { Message = $"Unknown story task type: {task.Type}" });
            }

            task.Status = "approved";
            task.ApprovedByUserId = currentUser?.Id;
            task.ResolvedOnDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _taskRepository.UpdateAsync(task);

            await _auditService.LogAsync(companyId, projectId, "MaxStoryTaskApproved", "KanbanCard", cardId, _appUser, $"Type: {task.Type}");
            return Ok(_mapper.Map<MaxTaskResponse>(task));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving Max story task {TaskId}", taskId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("tasks/{taskId}/reject")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxTaskResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectTask(string companyId, string projectId, string cardId, string taskId)
    {
        try
        {
            var card = await _cardRepository.GetAsync(cardId);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            var task = await _taskRepository.GetAsync(taskId);
            if (task == null || task.StoryId != cardId) return NotFound();
            if (task.Status != "pending")
                return BadRequest(new BadRequestResponse { Message = $"Task is already {task.Status}." });

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            task.Status = "rejected";
            task.ApprovedByUserId = currentUser?.Id;
            task.ResolvedOnDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _taskRepository.UpdateAsync(task);

            return Ok(_mapper.Map<MaxTaskResponse>(task));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting Max story task {TaskId}", taskId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ----- Approve handler: merge_story_duplicate -----

    /// Mark the source card as merged into the target:
    ///  - append source.LinkedTicketIds onto target (dedup)
    ///  - append source.ExternalSources onto target (preserves the Discord link)
    ///  - drop a system comment on the target so there's an audit trail
    ///  - soft-delete the source card
    /// Refuses if the target is missing or resolves to the source itself (defense in depth).
    private async Task<(bool ok, string? error)> ExecuteStoryMergeAsync(KanbanCard sourceCard, MaxTask task, User? currentUser)
    {
        var targetId = task.Details?.DuplicateOfStoryId;
        if (string.IsNullOrEmpty(targetId))
            return (false, "Task is missing a duplicate target id.");
        if (targetId == sourceCard.Id)
            return (false, "Cannot merge a story into itself.");

        var target = await _cardRepository.GetAsync(targetId);
        if (target == null || target.IsVoid)
            return (false, "Duplicate target card no longer exists.");
        if (target.ProjectId != sourceCard.ProjectId)
            return (false, "Cannot merge stories across projects.");

        // Carry forward linked tickets.
        if (sourceCard.LinkedTicketIds?.Count > 0)
        {
            target.LinkedTicketIds ??= new List<string>();
            foreach (var t in sourceCard.LinkedTicketIds)
            {
                if (!target.LinkedTicketIds.Contains(t)) target.LinkedTicketIds.Add(t);
            }
        }

        // Carry forward linked stories — drop self-references (target linking to itself) and the
        // source's own id (target was about to link to a card it's swallowing).
        if (sourceCard.LinkedStoryIds?.Count > 0)
        {
            target.LinkedStoryIds ??= new List<string>();
            foreach (var s in sourceCard.LinkedStoryIds)
            {
                if (s == target.Id || s == sourceCard.Id) continue;
                if (!target.LinkedStoryIds.Contains(s)) target.LinkedStoryIds.Add(s);
            }
        }

        // Carry forward external source pins (e.g. the Discord link). Dedup on messageId+type.
        if (sourceCard.ExternalSources?.Count > 0)
        {
            target.ExternalSources ??= new List<KanbanCardExternalSource>();
            foreach (var src in sourceCard.ExternalSources)
            {
                bool already = target.ExternalSources.Any(x =>
                    x.Type == src.Type &&
                    string.Equals(x.MessageId, src.MessageId, StringComparison.Ordinal));
                if (!already) target.ExternalSources.Add(src);
            }
        }

        await _cardRepository.UpdateAsync(target);

        // Audit trail on the survivor.
        await _commentRepository.CreateAsync(new KanbanCardComment
        {
            CardId = target.Id,
            CompanyId = target.CompanyId,
            ProjectId = target.ProjectId,
            Type = KanbanCommentType.System,
            AuthorName = currentUser?.DisplayName,
            BodyHtml = $"<p>Merged STR-{sourceCard.CardNumber} \"{System.Net.WebUtility.HtmlEncode(sourceCard.Title)}\" into this card via Max.</p>",
        });

        // Soft-delete the duplicate.
        await _cardRepository.RemoveAsync(sourceCard.Id);

        return (true, null);
    }
}
