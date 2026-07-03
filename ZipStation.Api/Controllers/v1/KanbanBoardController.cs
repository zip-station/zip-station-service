using System.Text.RegularExpressions;
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
[Route("api/v{version:apiVersion}/companies/{companyId}/projects/{projectId}/board")]
[Authorize]
public class KanbanBoardController : BaseController
{
    private const int MaxColumns = 8;
    private const double PositionStep = 1000d;
    private const long MaxImageSize = 5 * 1024 * 1024; // 5 MB
    private static readonly TimeSpan ImageUrlLifetime = TimeSpan.FromHours(1);
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp",
    };
    private static readonly Regex ImgTagPattern = new(@"<img\b[^>]*?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DataKeyAttrPattern = new(@"data-zs-key=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SrcAttrPattern = new(@"\bsrc=""[^""]*""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly (string Name, string Color)[] DefaultColumns =
    {
        ("Backlog", "#94a3b8"),
        ("Ready", "#60a5fa"),
        ("Committed", "#a78bfa"),
        ("Active", "#f59e0b"),
        ("Done", "#22c55e"),
    };

    private readonly ILogger<KanbanBoardController> _logger;
    private readonly IKanbanBoardRepository _boardRepository;
    private readonly IKanbanCardRepository _cardRepository;
    private readonly IKanbanCardCommentRepository _commentRepository;
    private readonly IKanbanCardNumberCounterRepository _cardNumberCounterRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketMessageRepository _ticketMessageRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IAppUser _appUser;
    private readonly IKanbanBoardGateway _gateway;
    private readonly IAuditService _auditService;
    private readonly IFileStorageService _fileStorageService;

    public KanbanBoardController(
        ILogger<KanbanBoardController> logger,
        IKanbanBoardRepository boardRepository,
        IKanbanCardRepository cardRepository,
        IKanbanCardCommentRepository commentRepository,
        IKanbanCardNumberCounterRepository cardNumberCounterRepository,
        ITicketRepository ticketRepository,
        ITicketMessageRepository ticketMessageRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IMapper mapper,
        IAppUser appUser,
        IKanbanBoardGateway gateway,
        IAuditService auditService,
        IFileStorageService fileStorageService)
    {
        _logger = logger;
        _boardRepository = boardRepository;
        _cardRepository = cardRepository;
        _commentRepository = commentRepository;
        _cardNumberCounterRepository = cardNumberCounterRepository;
        _ticketRepository = ticketRepository;
        _ticketMessageRepository = ticketMessageRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _appUser = appUser;
        _gateway = gateway;
        _auditService = auditService;
        _fileStorageService = fileStorageService;
    }

    // ----- Board -----

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanBoardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBoard(string companyId, string projectId)
    {
        try
        {
            var gw = await _gateway.CanViewAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            var board = await GetOrCreateBoardAsync(companyId, projectId);
            return Ok(_mapper.Map<KanbanBoardResponse>(board));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading kanban board for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("columns")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanBoardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateColumns(string companyId, string projectId, [FromBody] UpdateKanbanColumnsRequest request)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            if (request.Columns == null || request.Columns.Count == 0)
                return BadRequest(new BadRequestResponse { Message = "Board must have at least one column" });

            if (request.Columns.Count > MaxColumns)
                return BadRequest(new BadRequestResponse { Message = $"Board cannot have more than {MaxColumns} columns" });

            var board = await GetOrCreateBoardAsync(companyId, projectId);
            var existingById = board.Columns.ToDictionary(c => c.Id);

            var newColumns = new List<KanbanColumn>();
            for (var i = 0; i < request.Columns.Count; i++)
            {
                var input = request.Columns[i];
                if (string.IsNullOrWhiteSpace(input.Name))
                    return BadRequest(new BadRequestResponse { Message = "Column name cannot be empty" });

                if (!string.IsNullOrEmpty(input.Id) && existingById.TryGetValue(input.Id, out var existing))
                {
                    existing.Name = input.Name.Trim();
                    existing.Color = input.Color;
                    existing.Position = i;
                    newColumns.Add(existing);
                }
                else
                {
                    newColumns.Add(new KanbanColumn
                    {
                        Name = input.Name.Trim(),
                        Color = input.Color,
                        Position = i,
                    });
                }
            }

            // Detect removed columns — reject if any cards still live there
            var newIds = newColumns.Select(c => c.Id).ToHashSet();
            var removedIds = board.Columns.Where(c => !newIds.Contains(c.Id)).Select(c => c.Id).ToList();
            foreach (var removedId in removedIds)
            {
                if (await _cardRepository.AnyInColumnAsync(board.Id, removedId))
                    return BadRequest(new BadRequestResponse { Message = "Move or delete cards before removing a column" });
            }

            board.Columns = newColumns;

            // Validate resolved column still exists
            if (string.IsNullOrEmpty(request.ResolvedColumnId) || !newIds.Contains(request.ResolvedColumnId))
            {
                // Fall back to existing if still valid, else the last column
                if (!newIds.Contains(board.ResolvedColumnId))
                    board.ResolvedColumnId = newColumns[^1].Id;
            }
            else
            {
                board.ResolvedColumnId = request.ResolvedColumnId;
            }

            // Reconcile custom story types when provided. Null = caller isn't managing types.
            if (request.CardTypes != null)
            {
                var typesError = await ReconcileCustomCardTypesAsync(board, request.CardTypes);
                if (typesError != null)
                    return BadRequest(new BadRequestResponse { Message = typesError });
            }

            await _boardRepository.UpdateAsync(board);
            await _auditService.LogAsync(companyId, projectId, "KanbanColumnsUpdated", "KanbanBoard", board.Id, _appUser);
            return Ok(_mapper.Map<KanbanBoardResponse>(board));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating kanban columns for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("resolved-column")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanBoardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetResolvedColumn(string companyId, string projectId, [FromBody] SetResolvedColumnRequest request)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var board = await GetOrCreateBoardAsync(companyId, projectId);
            if (!board.Columns.Any(c => c.Id == request.ColumnId))
                return BadRequest(new BadRequestResponse { Message = "Column not found on this board" });

            board.ResolvedColumnId = request.ColumnId;
            await _boardRepository.UpdateAsync(board);
            return Ok(_mapper.Map<KanbanBoardResponse>(board));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting resolved column for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ----- Cards -----

    [HttpGet("cards")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<KanbanCardResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCards(
        string companyId,
        string projectId,
        [FromQuery] string? query = null,
        [FromQuery] string? columnId = null,
        [FromQuery] string? assignedTo = null,
        [FromQuery] string? type = null,
        [FromQuery] List<string>? tags = null,
        [FromQuery] bool? hasLinkedTickets = null,
        [FromQuery] long? createdSince = null)
    {
        try
        {
            var gw = await _gateway.CanViewAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var board = await GetOrCreateBoardAsync(companyId, projectId);

            var resolvedAssignee = assignedTo;
            if (assignedTo == "me")
            {
                var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
                resolvedAssignee = currentUser?.Id;
            }

            // If the search text is a recognized external-source link (e.g. a Discord forum
            // post), match it against each card's ExternalSources instead of title/description.
            var textQuery = query;
            KanbanCardExternalSource? externalSource = null;
            if (!string.IsNullOrWhiteSpace(query))
            {
                externalSource = ExternalSourceSearch.Parse(query.Trim());
                if (externalSource != null) textQuery = null;
            }

            // The board only renders committed (in-progress) and resolved work; backlog,
            // unreviewed, archived and obsolete stories live in the cross-project backlog grid.
            var cards = await _cardRepository.SearchAsync(
                board.Id, textQuery, columnId, resolvedAssignee, type, tags, hasLinkedTickets,
                createdSince, KanbanStatusRules.BoardStatuses, externalSource);

            return Ok(_mapper.Map<List<KanbanCardResponse>>(cards));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing kanban cards for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("cards")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCard(string companyId, string projectId, [FromBody] CreateKanbanCardRequest request)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new BadRequestResponse { Message = "Title is required" });

            var board = await GetOrCreateBoardAsync(companyId, projectId);

            var cardType = NormalizeCardType(board, request.Type);
            if (cardType == null)
                return BadRequest(new BadRequestResponse { Message = "Unknown story type" });

            // Resolve the lifecycle status first: an explicit request wins; otherwise a supplied
            // board column implies Committed (on the board), and no column implies Backlog (off it).
            var explicitColumn = !string.IsNullOrEmpty(request.ColumnId) && board.Columns.Any(c => c.Id == request.ColumnId);
            var status = request.Status ?? (explicitColumn ? KanbanStoryStatus.Committed : KanbanStoryStatus.Backlog);
            // A card dropped straight into the resolved column is Resolved.
            if (explicitColumn && request.ColumnId == board.ResolvedColumnId) status = KanbanStoryStatus.Resolved;

            // On-board statuses (Committed/Resolved) live in a column; off-board statuses carry none.
            // When committed without an explicit column, land in the board's first workflow column.
            var targetColumnId = KanbanStatusRules.IsOnBoard(status)
                ? (explicitColumn ? request.ColumnId : (KanbanStatusRules.ResolveCommitColumnId(board) ?? string.Empty))
                : string.Empty;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var minPos = string.IsNullOrEmpty(targetColumnId)
                ? (double?)null
                : await _cardRepository.GetMinPositionInColumnAsync(board.Id, targetColumnId);
            var cardNumber = await _cardNumberCounterRepository.GetNextCardNumberAsync(projectId);
            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            var card = new KanbanCard
            {
                CompanyId = companyId,
                ProjectId = projectId,
                BoardId = board.Id,
                CardNumber = cardNumber,
                ColumnId = targetColumnId,
                Position = (minPos ?? PositionStep) - PositionStep,
                Status = status,
                // Explicit rank (add-to-top/bottom from the backlog grid) wins; otherwise seed from
                // creation time so new items append in order. Drag-to-prioritize rewrites it later.
                BacklogPosition = request.BacklogPosition ?? now,
                Title = request.Title.Trim(),
                DescriptionHtml = request.DescriptionHtml,
                Type = cardType,
                Priority = request.Priority,
                Tags = request.Tags ?? new List<string>(),
                AssignedToUserId = request.AssignedToUserId,
                LinkedTicketIds = request.LinkedTicketIds ?? new List<string>(),
                CreatedByUserId = currentUser?.Id,
                ResolvedOnDateTime = status == KanbanStoryStatus.Resolved ? now : 0,
            };

            var created = await _cardRepository.CreateAsync(card);
            await _auditService.LogAsync(companyId, projectId, "Created", "KanbanCard", created.Id, _appUser, $"STR-{cardNumber}: {card.Title}");

            if (created.Status == KanbanStoryStatus.Resolved && created.LinkedTicketIds.Count > 0)
                await NotifyLinkedTicketsAsync(created, companyId, currentUser, "marked linked story", "as resolved.");

            return Ok(_mapper.Map<KanbanCardResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating kanban card in project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("cards/{cardNumber:long}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCard(string companyId, string projectId, long cardNumber)
    {
        try
        {
            var gw = await _gateway.CanViewAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var card = await _cardRepository.GetByCardNumberAsync(projectId, cardNumber);
            if (card == null || card.CompanyId != companyId) return NotFound();

            var comments = await _commentRepository.GetByCardIdAsync(card.Id);
            var linkedTickets = await LoadLinkedTicketsAsync(card.LinkedTicketIds, companyId);
            var linkedStories = await LoadLinkedStoriesAsync(card.LinkedStoryIds, companyId);

            var project = await _projectRepository.GetAsync(projectId);
            var fileStorage = project?.Settings?.FileStorage;
            var board = await _boardRepository.GetByProjectIdAsync(projectId);
            var columnNamesById = board?.Columns.ToDictionary(c => c.Id, c => c.Name) ?? new Dictionary<string, string>();

            var cardResponse = _mapper.Map<KanbanCardResponse>(card);
            cardResponse.DescriptionHtml = RefreshImageUrls(cardResponse.DescriptionHtml, fileStorage);

            var commentResponses = _mapper.Map<List<KanbanCardCommentResponse>>(comments);
            foreach (var c in commentResponses)
                c.BodyHtml = RefreshImageUrls(c.BodyHtml, fileStorage) ?? string.Empty;

            var linkedStorySummaries = linkedStories.Select(s => new KanbanStorySummaryResponse
            {
                Id = s.Id,
                ProjectId = s.ProjectId,
                CardNumber = s.CardNumber,
                Title = s.Title,
                Type = s.Type,
                Priority = s.Priority,
                ColumnId = s.ColumnId,
                ColumnName = columnNamesById.TryGetValue(s.ColumnId, out var cn) ? cn : null,
                AssignedToUserId = s.AssignedToUserId,
            }).ToList();

            return Ok(new KanbanCardDetailResponse
            {
                Card = cardResponse,
                Comments = commentResponses,
                LinkedTickets = _mapper.Map<List<TicketResponse>>(linkedTickets),
                LinkedStories = linkedStorySummaries,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting kanban card {CardNumber}", cardNumber);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("images")]
    [MapToApiVersion("1.0")]
    [RequestSizeLimit(MaxImageSize + 1024)]
    [ProducesResponseType(typeof(KanbanImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(string companyId, string projectId, IFormFile file)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            if (file == null || file.Length == 0)
                return BadRequest(new BadRequestResponse { Message = "No file provided" });
            if (file.Length > MaxImageSize)
                return BadRequest(new BadRequestResponse { Message = "Image exceeds 5MB limit" });
            if (!AllowedImageContentTypes.Contains(file.ContentType))
                return BadRequest(new BadRequestResponse { Message = "Unsupported image type. Use PNG, JPEG, GIF, or WebP." });

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();
            if (project.Settings?.FileStorage == null || string.IsNullOrEmpty(project.Settings.FileStorage.BucketName))
                return BadRequest(new BadRequestResponse { Message = "File storage not configured for this project" });

            var safeName = SanitizeFileName(file.FileName);
            var storageKey = $"{companyId}/{projectId}/kanban-images/{MongoDB.Bson.ObjectId.GenerateNewId()}_{safeName}";

            using var stream = file.OpenReadStream();
            await _fileStorageService.UploadAsync(project.Settings.FileStorage, storageKey, stream, file.ContentType);

            var url = _fileStorageService.GeneratePresignedUrl(project.Settings.FileStorage, storageKey, ImageUrlLifetime);
            return Ok(new KanbanImageUploadResponse { StorageKey = storageKey, Url = url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading kanban image for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("cards/{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCard(string companyId, string projectId, string id, [FromBody] UpdateKanbanCardRequest request)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var card = await _cardRepository.GetAsync(id);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            var board = await GetOrCreateBoardAsync(companyId, projectId);
            var previousColumnId = card.ColumnId;
            var previousStatus = card.Status;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (request.Title != null) card.Title = request.Title.Trim();
            if (request.DescriptionHtml != null) card.DescriptionHtml = request.DescriptionHtml;
            if (request.Type != null)
            {
                var cardType = NormalizeCardType(board, request.Type);
                if (cardType == null)
                    return BadRequest(new BadRequestResponse { Message = "Unknown story type" });
                card.Type = cardType;
            }
            if (request.Priority.HasValue) card.Priority = request.Priority.Value;
            if (request.Tags != null) card.Tags = request.Tags;
            if (request.ClearAssignee == true) card.AssignedToUserId = null;
            else if (request.AssignedToUserId != null) card.AssignedToUserId = request.AssignedToUserId;

            // Backlog grid ordering — independent of the on-board column position.
            if (request.BacklogPosition.HasValue) card.BacklogPosition = request.BacklogPosition.Value;

            if (request.Status.HasValue && request.Status.Value != card.Status)
            {
                // Explicit lifecycle transition (commit / obsolete / archive / resolve / send to
                // backlog / mark reviewed). The plan owns any column move + resolved-timestamp.
                var plan = KanbanStatusRules.PlanStatusChange(card, request.Status.Value, board, now);
                card.Status = plan.Status;
                if (plan.ClearColumn) card.ColumnId = string.Empty;
                else if (plan.MoveToColumnId != null) card.ColumnId = plan.MoveToColumnId;
                if (plan.PlaceAtBoardEntry)
                {
                    var maxPos = await _cardRepository.GetMaxPositionInColumnAsync(board.Id, card.ColumnId);
                    card.Position = maxPos + PositionStep;
                }
                if (plan.ResolvedChanged) card.ResolvedOnDateTime = plan.ResolvedOnDateTime;
            }
            else
            {
                // No explicit status — a drag on the board. Move the column, then sync status so
                // crossing the resolved column resolves/re-commits the card.
                if (request.ColumnId != null && request.ColumnId != card.ColumnId)
                {
                    if (!board.Columns.Any(c => c.Id == request.ColumnId))
                        return BadRequest(new BadRequestResponse { Message = "Column not found on this board" });
                    card.ColumnId = request.ColumnId;
                }

                if (request.Position.HasValue)
                {
                    card.Position = request.Position.Value;
                }
                else if (request.ColumnId != null && request.ColumnId != previousColumnId)
                {
                    var maxPos = await _cardRepository.GetMaxPositionInColumnAsync(board.Id, card.ColumnId);
                    card.Position = maxPos + PositionStep;
                }

                KanbanStatusRules.SyncStatusForColumnMove(card, previousColumnId, board, now);
            }

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            card.UpdatedByUserId = currentUser?.Id;

            var movedIntoResolved = previousStatus != KanbanStoryStatus.Resolved && card.Status == KanbanStoryStatus.Resolved;
            var movedOutOfResolved = previousStatus == KanbanStoryStatus.Resolved && card.Status != KanbanStoryStatus.Resolved;

            var updated = await _cardRepository.UpdateAsync(card);
            await _auditService.LogAsync(companyId, projectId, "Updated", "KanbanCard", updated.Id, _appUser, $"STR-{card.CardNumber}");

            if (movedIntoResolved && updated.LinkedTicketIds.Count > 0)
                await NotifyLinkedTicketsAsync(updated, companyId, currentUser, "marked linked story", "as resolved.");
            else if (movedOutOfResolved && updated.LinkedTicketIds.Count > 0)
                await NotifyLinkedTicketsAsync(updated, companyId, currentUser, "reopened linked story", string.Empty);

            return Ok(_mapper.Map<KanbanCardResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating kanban card {CardId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("cards/{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCard(string companyId, string projectId, string id)
    {
        try
        {
            var gw = await _gateway.CanDeleteAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var card = await _cardRepository.GetAsync(id);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            await _cardRepository.RemoveAsync(id);
            await _auditService.LogAsync(companyId, projectId, "Deleted", "KanbanCard", id, _appUser, $"STR-{card.CardNumber}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting kanban card {CardId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ----- Comments -----

    [HttpPost("cards/{id}/comments")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardCommentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddComment(string companyId, string projectId, string id, [FromBody] AddKanbanCardCommentRequest request)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            if (string.IsNullOrWhiteSpace(request.BodyHtml))
                return BadRequest(new BadRequestResponse { Message = "Comment body is required" });

            var card = await _cardRepository.GetAsync(id);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            var comment = new KanbanCardComment
            {
                CardId = id,
                CompanyId = companyId,
                ProjectId = projectId,
                Type = KanbanCommentType.User,
                AuthorUserId = currentUser?.Id,
                AuthorName = currentUser?.DisplayName,
                BodyHtml = request.BodyHtml,
            };

            var created = await _commentRepository.CreateAsync(comment);
            var response = _mapper.Map<KanbanCardCommentResponse>(created);
            var project = await _projectRepository.GetAsync(projectId);
            response.BodyHtml = RefreshImageUrls(response.BodyHtml, project?.Settings?.FileStorage) ?? string.Empty;
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to kanban card {CardId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("cards/{id}/comments/{commentId}")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> DeleteComment(string companyId, string projectId, string id, string commentId)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var comment = await _commentRepository.GetAsync(commentId);
            if (comment == null || comment.CardId != id) return NotFound();
            if (comment.Type == KanbanCommentType.System)
                return BadRequest(new BadRequestResponse { Message = "System comments cannot be deleted" });

            await _commentRepository.RemoveAsync(commentId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting kanban comment {CommentId}", commentId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ----- Linked tickets -----

    [HttpPost("cards/{id}/link-ticket")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LinkTicket(string companyId, string projectId, string id, [FromBody] LinkKanbanTicketRequest request)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var card = await _cardRepository.GetAsync(id);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            var ticket = await ResolveTicketAsync(companyId, request.TicketIdOrNumber);
            if (ticket == null)
                return BadRequest(new BadRequestResponse { Message = "Ticket not found. Enter a ticket number or ID." });

            if (!card.LinkedTicketIds.Contains(ticket.Id))
            {
                card.LinkedTicketIds.Add(ticket.Id);
                await _cardRepository.UpdateAsync(card);
            }

            return Ok(_mapper.Map<KanbanCardResponse>(card));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking ticket to kanban card {CardId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("cards/{id}/link-ticket/{ticketId}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlinkTicket(string companyId, string projectId, string id, string ticketId)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var card = await _cardRepository.GetAsync(id);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            if (card.LinkedTicketIds.Remove(ticketId))
                await _cardRepository.UpdateAsync(card);

            return Ok(_mapper.Map<KanbanCardResponse>(card));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking ticket from kanban card {CardId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ----- Linked stories -----

    [HttpPost("cards/{id}/link-story")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LinkStory(string companyId, string projectId, string id, [FromBody] LinkKanbanStoryRequest request)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var card = await _cardRepository.GetAsync(id);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            var target = await ResolveCardAsync(projectId, request.CardIdOrNumber);
            if (target == null)
                return BadRequest(new BadRequestResponse { Message = "Story not found. Enter a card number (e.g. 42) or ID." });
            if (target.Id == card.Id)
                return BadRequest(new BadRequestResponse { Message = "Can't link a story to itself." });

            if (!card.LinkedStoryIds.Contains(target.Id))
            {
                card.LinkedStoryIds.Add(target.Id);
                await _cardRepository.UpdateAsync(card);
            }

            return Ok(_mapper.Map<KanbanCardResponse>(card));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking story to kanban card {CardId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("cards/{id}/link-story/{otherCardId}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlinkStory(string companyId, string projectId, string id, string otherCardId)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var card = await _cardRepository.GetAsync(id);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            if (card.LinkedStoryIds.Remove(otherCardId))
                await _cardRepository.UpdateAsync(card);

            return Ok(_mapper.Map<KanbanCardResponse>(card));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking story from kanban card {CardId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ----- External sources (manual link to Discord posts etc.) -----

    [HttpPost("cards/{id}/external-source")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddExternalSource(string companyId, string projectId, string id, [FromBody] AddExternalSourceRequest request)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var card = await _cardRepository.GetAsync(id);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            var url = (request.Url ?? "").Trim();
            if (string.IsNullOrEmpty(url))
                return BadRequest(new BadRequestResponse { Message = "Paste a URL to link." });

            // Recognized sources (currently Discord) get rich parsing; anything else that's a
            // valid http/https URL is pinned as a generic Link.
            var parsed = ExternalSourceSearch.Parse(url);
            if (parsed == null)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    return BadRequest(new BadRequestResponse { Message = "Enter a valid link starting with http:// or https://" });

                parsed = new KanbanCardExternalSource { Type = ExternalSourceType.Link, Url = url };
            }

            // Idempotent: dedup Discord by messageId (matches the poll's key), generic links by URL.
            var alreadyLinked = parsed.Type == ExternalSourceType.Discord && !string.IsNullOrEmpty(parsed.MessageId)
                ? card.ExternalSources.Any(s => s.Type == parsed.Type && string.Equals(s.MessageId, parsed.MessageId, StringComparison.Ordinal))
                : card.ExternalSources.Any(s => string.Equals(s.Url, parsed.Url, StringComparison.OrdinalIgnoreCase));
            if (alreadyLinked)
                return Ok(_mapper.Map<KanbanCardResponse>(card)); // already linked

            card.ExternalSources.Add(parsed);
            await _cardRepository.UpdateAsync(card);
            await _auditService.LogAsync(companyId, projectId, "ExternalSourceLinked", "KanbanCard", card.Id, _appUser, parsed.Url);

            return Ok(_mapper.Map<KanbanCardResponse>(card));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding external source to kanban card {CardId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("cards/{id}/external-source")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(KanbanCardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveExternalSource(string companyId, string projectId, string id, [FromQuery] string url)
    {
        try
        {
            var gw = await _gateway.CanEditAsync(companyId, projectId);
            if (gw.ResponseStatus != GatewayResponseCodes.Ok) return ProcessGatewayResponse(gw);

            var card = await _cardRepository.GetAsync(id);
            if (card == null || card.CompanyId != companyId || card.ProjectId != projectId) return NotFound();

            // Keyed by URL so it works for every source type (Discord and generic links alike).
            var before = card.ExternalSources.Count;
            card.ExternalSources.RemoveAll(s => string.Equals(s.Url, url, StringComparison.Ordinal));
            if (card.ExternalSources.Count != before)
            {
                await _cardRepository.UpdateAsync(card);
                await _auditService.LogAsync(companyId, projectId, "ExternalSourceUnlinked", "KanbanCard", card.Id, _appUser, url);
            }

            return Ok(_mapper.Map<KanbanCardResponse>(card));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing external source from kanban card {CardId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ----- Helpers -----

    /// Resolves an incoming story-type value to a stored key. Empty resolves to the default
    /// built-in; a built-in name or a custom type id defined on this board passes through;
    /// anything else returns null (caller rejects with 400).
    private static string? NormalizeCardType(KanbanBoard board, string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return KanbanCardTypes.Feature;
        var trimmed = type.Trim();
        if (KanbanCardTypes.IsBuiltIn(trimmed)) return trimmed;
        return board.CustomCardTypes.Any(t => t.Id == trimmed) ? trimmed : null;
    }

    /// Applies the requested custom-type set to the board: preserves existing types by id (so
    /// renames/recolors don't rewrite any cards), creates new ones, and refuses to remove a type
    /// that cards still use. Returns an error message on failure, or null on success.
    private async Task<string?> ReconcileCustomCardTypesAsync(KanbanBoard board, List<KanbanCardTypeInput> inputs)
    {
        var existingById = board.CustomCardTypes.ToDictionary(t => t.Id);
        var newTypes = new List<KanbanCardTypeDefinition>();
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            var label = (input.Label ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(label))
                return "Story type name cannot be empty";
            if (KanbanCardTypes.IsBuiltIn(label))
                return $"\"{label}\" is already a built-in story type";
            if (!seenLabels.Add(label))
                return $"Duplicate story type \"{label}\"";

            if (!string.IsNullOrEmpty(input.Id) && existingById.TryGetValue(input.Id, out var existing))
            {
                existing.Label = label;
                existing.Color = input.Color;
                newTypes.Add(existing);
            }
            else
            {
                newTypes.Add(new KanbanCardTypeDefinition { Label = label, Color = input.Color });
            }
        }

        var newIds = newTypes.Select(t => t.Id).ToHashSet();
        var removedIds = board.CustomCardTypes.Where(t => !newIds.Contains(t.Id)).Select(t => t.Id).ToList();
        foreach (var removedId in removedIds)
        {
            if (await _cardRepository.AnyWithTypeAsync(board.Id, removedId))
                return "Reassign cards before removing a story type";
        }

        board.CustomCardTypes = newTypes;
        return null;
    }

    private async Task<KanbanBoard> GetOrCreateBoardAsync(string companyId, string projectId)
    {
        var board = await _boardRepository.GetByProjectIdAsync(projectId);
        if (board != null) return board;

        var columns = DefaultColumns.Select((c, i) => new KanbanColumn
        {
            Name = c.Name,
            Color = c.Color,
            Position = i,
        }).ToList();

        var newBoard = new KanbanBoard
        {
            CompanyId = companyId,
            ProjectId = projectId,
            Columns = columns,
            ResolvedColumnId = columns[^1].Id,
        };
        return await _boardRepository.CreateAsync(newBoard);
    }

    private async Task<Ticket?> ResolveTicketAsync(string companyId, string idOrNumber)
    {
        if (string.IsNullOrWhiteSpace(idOrNumber)) return null;
        var byId = await _ticketRepository.GetAsync(idOrNumber);
        if (byId != null && byId.CompanyId == companyId) return byId;

        if (long.TryParse(idOrNumber.TrimStart('#').TrimStart('0'), out var num) && num > 0)
        {
            var byNum = await _ticketRepository.GetByTicketNumberAsync(companyId, num);
            if (byNum != null) return byNum;
        }
        return null;
    }

    private async Task<List<Ticket>> LoadLinkedTicketsAsync(List<string> ticketIds, string companyId)
    {
        var result = new List<Ticket>();
        foreach (var tid in ticketIds)
        {
            var ticket = await _ticketRepository.GetAsync(tid);
            if (ticket != null && ticket.CompanyId == companyId)
                result.Add(ticket);
        }
        return result;
    }

    /// Match Discord URL forms:
    ///   - https://discord.com/channels/{guild}/{x}           (2 segments — x is a thread or channel id)
    ///   - https://discord.com/channels/{guild}/{channel}/{m} (3 segments — full message URL)
    /// Also tolerates discordapp.com (legacy) and ptb./canary. subdomains.
    private async Task<List<KanbanCard>> LoadLinkedStoriesAsync(List<string> cardIds, string companyId)
    {
        // Skip self-references / already-voided cards. Stale ids are tolerated silently —
        // they accumulate when the maintainer deletes a referenced card, and re-linking
        // would be more annoying than just hiding them.
        var result = new List<KanbanCard>();
        foreach (var cid in cardIds.Distinct())
        {
            var card = await _cardRepository.GetAsync(cid);
            if (card != null && !card.IsVoid && card.CompanyId == companyId)
                result.Add(card);
        }
        return result;
    }

    private async Task<KanbanCard?> ResolveCardAsync(string projectId, string idOrNumber)
    {
        if (string.IsNullOrWhiteSpace(idOrNumber)) return null;
        var trimmed = idOrNumber.Trim().TrimStart('#').TrimStart('S', 's').TrimStart('T', 't').TrimStart('R', 'r').TrimStart('-');
        if (long.TryParse(trimmed, out var num) && num > 0)
        {
            var byNum = await _cardRepository.GetByCardNumberAsync(projectId, num);
            if (byNum != null) return byNum;
        }
        // Fall back to literal id lookup.
        var byId = await _cardRepository.GetAsync(idOrNumber.Trim());
        return byId;
    }

    private string? RefreshImageUrls(string? html, FileStorageSettings? settings)
    {
        if (string.IsNullOrEmpty(html) || settings == null || string.IsNullOrEmpty(settings.BucketName))
            return html;

        return ImgTagPattern.Replace(html, match =>
        {
            var imgTag = match.Value;
            var keyMatch = DataKeyAttrPattern.Match(imgTag);
            if (!keyMatch.Success) return imgTag;

            var storageKey = System.Net.WebUtility.HtmlDecode(keyMatch.Groups[1].Value);
            string url;
            try
            {
                url = _fileStorageService.GeneratePresignedUrl(settings, storageKey, ImageUrlLifetime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate presigned URL for kanban image {StorageKey}", storageKey);
                return imgTag;
            }

            var escapedUrl = System.Net.WebUtility.HtmlEncode(url);
            return SrcAttrPattern.IsMatch(imgTag)
                ? SrcAttrPattern.Replace(imgTag, $@"src=""{escapedUrl}""", 1)
                : imgTag.Replace("<img", $@"<img src=""{escapedUrl}""", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "image";
        var name = System.IO.Path.GetFileName(fileName);
        var clean = new string(name.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_').ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "image" : clean;
    }

    private async Task NotifyLinkedTicketsAsync(KanbanCard card, string companyId, User? actor, string verb, string suffix)
    {
        var actorName = actor?.DisplayName ?? "Someone";
        var trailing = string.IsNullOrEmpty(suffix) ? "." : $" {suffix}";
        foreach (var ticketId in card.LinkedTicketIds)
        {
            try
            {
                var ticket = await _ticketRepository.GetAsync(ticketId);
                if (ticket == null || ticket.CompanyId != companyId) continue;

                var systemMsg = new TicketMessage
                {
                    TicketId = ticket.Id,
                    CompanyId = ticket.CompanyId,
                    ProjectId = ticket.ProjectId,
                    Body = $"{actorName} {verb} STR-{card.CardNumber} \"{card.Title}\"{trailing}",
                    IsInternalNote = false,
                    Source = MessageSource.System,
                };
                await _ticketMessageRepository.CreateAsync(systemMsg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to post system note to linked ticket {TicketId} for card {CardId}", ticketId, card.Id);
            }
        }
    }
}

public class UpdateKanbanColumnsRequest
{
    public List<KanbanColumnInput> Columns { get; set; } = new();
    public string ResolvedColumnId { get; set; } = string.Empty;

    /// Custom (project-specific) story types to persist on the board. Null means "leave the
    /// existing custom types untouched" — so callers that only manage columns (e.g. the MCP
    /// update_columns tool) don't have to send them.
    public List<KanbanCardTypeInput>? CardTypes { get; set; }
}

public class KanbanColumnInput
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}

public class KanbanCardTypeInput
{
    /// Existing custom type id to update, or null/empty to create a new one.
    public string? Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Color { get; set; }
}

public class SetResolvedColumnRequest
{
    public string ColumnId { get; set; } = string.Empty;
}

public class CreateKanbanCardRequest
{
    public string ColumnId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public string Type { get; set; } = KanbanCardTypes.Feature;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public List<string>? Tags { get; set; }
    public string? AssignedToUserId { get; set; }
    public List<string>? LinkedTicketIds { get; set; }

    /// Lifecycle bucket to create the card in. Null lets the server choose: Committed when a board
    /// ColumnId is supplied, otherwise Backlog.
    public KanbanStoryStatus? Status { get; set; }

    /// Explicit backlog rank (e.g. add-to-top / add-to-bottom from the backlog grid). Null seeds
    /// it from creation time so new items append in order.
    public double? BacklogPosition { get; set; }
}

public class UpdateKanbanCardRequest
{
    public string? Title { get; set; }
    public string? DescriptionHtml { get; set; }
    public string? Type { get; set; }
    public TicketPriority? Priority { get; set; }
    public List<string>? Tags { get; set; }
    public string? AssignedToUserId { get; set; }
    public bool? ClearAssignee { get; set; }
    public string? ColumnId { get; set; }
    public double? Position { get; set; }

    /// Explicit lifecycle transition. When set, the server applies the commit/obsolete/archive/
    /// resolve/backlog move (including any column change) and ignores ColumnId/Position.
    public KanbanStoryStatus? Status { get; set; }

    /// Hand-ordering rank within the backlog grid (drag-to-prioritize).
    public double? BacklogPosition { get; set; }
}

public class AddKanbanCardCommentRequest
{
    public string BodyHtml { get; set; } = string.Empty;
}

public class LinkKanbanTicketRequest
{
    public string TicketIdOrNumber { get; set; } = string.Empty;
}

public class LinkKanbanStoryRequest
{
    /// Either the target card's id (24-hex ObjectId) or its card number (numeric string).
    public string CardIdOrNumber { get; set; } = string.Empty;
}

public class AddExternalSourceRequest
{
    /// A URL pointing at the external resource. v1 only recognizes Discord URLs;
    /// unrecognized patterns are rejected so we can grow the parser per source type.
    public string Url { get; set; } = string.Empty;
}

public class KanbanImageUploadResponse
{
    public string StorageKey { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
