using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;
using ZipStation.Models.Responses;
using ZipStation.Models.SearchProfiles;
using ZipStation.Business.Services;
using MongoDB.Driver;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/tickets")]
[Authorize]
public class TicketsController : BaseController
{
    private readonly ILogger<TicketsController> _logger;
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketMessageRepository _ticketMessageRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITicketIdCounterRepository _ticketIdCounterRepository;
    private readonly ITicketDraftRepository _ticketDraftRepository;
    private readonly IMapper _mapper;
    private readonly IAppUser _appUser;
    private readonly ITicketGateway _ticketGateway;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly IAlertService _alertService;
    private readonly IMongoDatabase _database;

    public TicketsController(
        ILogger<TicketsController> logger,
        ITicketRepository ticketRepository,
        ITicketMessageRepository ticketMessageRepository,
        IUserRepository userRepository,
        IProjectRepository projectRepository,
        ITicketIdCounterRepository ticketIdCounterRepository,
        ITicketDraftRepository ticketDraftRepository,
        IMapper mapper,
        IAppUser appUser,
        ITicketGateway ticketGateway,
        IEmailService emailService,
        IAuditService auditService,
        IAlertService alertService,
        IMongoDatabase database)
    {
        _logger = logger;
        _ticketRepository = ticketRepository;
        _ticketMessageRepository = ticketMessageRepository;
        _userRepository = userRepository;
        _projectRepository = projectRepository;
        _ticketIdCounterRepository = ticketIdCounterRepository;
        _ticketDraftRepository = ticketDraftRepository;
        _mapper = mapper;
        _appUser = appUser;
        _ticketGateway = ticketGateway;
        _emailService = emailService;
        _auditService = auditService;
        _alertService = alertService;
        _database = database;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PaginatedResponse<TicketResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTickets(
        string companyId,
        [FromQuery] string? projectId = null,
        [FromQuery] List<TicketStatus>? status = null,
        [FromQuery] TicketPriority? priority = null,
        [FromQuery] string? query = null,
        [FromQuery] string? assignedTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int resultsPerPage = 25)
    {
        try
        {
            var gatewayResponse = await _ticketGateway.CanListTicketsAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var filter = Builders<Ticket>.Filter.Eq(t => t.CompanyId, companyId);

            if (!string.IsNullOrEmpty(projectId))
                filter &= Builders<Ticket>.Filter.Eq(t => t.ProjectId, projectId);

            if (status != null && status.Count > 0)
                filter &= Builders<Ticket>.Filter.In(t => t.Status, status);

            if (priority.HasValue)
                filter &= Builders<Ticket>.Filter.Eq(t => t.Priority, priority.Value);

            // Assignment filter
            if (assignedTo == "me")
            {
                var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
                if (currentUser != null)
                    filter &= Builders<Ticket>.Filter.Eq(t => t.AssignedToUserId, currentUser.Id);
            }
            else if (assignedTo == "unassigned")
                filter &= Builders<Ticket>.Filter.Eq(t => t.AssignedToUserId, null);
            else if (!string.IsNullOrEmpty(assignedTo))
                filter &= Builders<Ticket>.Filter.Eq(t => t.AssignedToUserId, assignedTo);

            // Search across subject, customerName, customerEmail, ticketNumber
            // Each word must match at least one field (AND between words, OR between fields)
            if (!string.IsNullOrWhiteSpace(query))
            {
                var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var term in terms)
                {
                    var regex = new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(term), "i");
                    var termFilters = new List<FilterDefinition<Ticket>>
                    {
                        Builders<Ticket>.Filter.Regex(t => t.Subject, regex),
                        Builders<Ticket>.Filter.Regex(t => t.CustomerEmail, regex),
                        Builders<Ticket>.Filter.Regex(t => t.CustomerName, regex)
                    };

                    // Try numeric match for ticket number
                    if (long.TryParse(term.TrimStart('0'), out var ticketNum) && ticketNum > 0)
                        termFilters.Add(Builders<Ticket>.Filter.Eq(t => t.TicketNumber, ticketNum));

                    filter &= Builders<Ticket>.Filter.Or(termFilters);
                }
            }

            var searchProfile = new BaseSearchProfile
            {
                Page = page,
                ResultsPerPage = resultsPerPage,
                OrderByFieldName = "createdOnDateTime",
                OrderByAscending = false
            };

            var result = await _ticketRepository.GetPaginatedResults(filter, searchProfile);

            return Ok(new PaginatedResponse<TicketResponse>
            {
                TotalResultCount = result.TotalResultCount,
                Results = _mapper.Map<List<TicketResponse>>(result.Results)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing tickets for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicket(string companyId, string id)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(id);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _ticketGateway.CanGetTicketAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var messages = await _ticketMessageRepository.GetByTicketIdAsync(id);

            return Ok(new TicketDetailResponse
            {
                Ticket = _mapper.Map<TicketResponse>(ticket),
                Messages = _mapper.Map<List<TicketMessageResponse>>(messages)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ticket {TicketId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTicket(string companyId, [FromBody] CreateTicketRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _ticketGateway.CanCreateTicketAsync(companyId, request.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Auto-generate ticket number
            var project = await _projectRepository.GetAsync(request.ProjectId);
            if (project == null)
                return BadRequest(new BadRequestResponse { Message = "Project not found" });

            var ticketIdSettings = project.Settings?.TicketId ?? new TicketIdSettings();
            var ticketNumber = await GenerateTicketNumberAsync(request.ProjectId, ticketIdSettings);

            // Generate display ID based on format settings
            var displayId = GenerateDisplayId(ticketNumber, ticketIdSettings);

            // Auto-generate subject from template if not provided
            var subject = string.IsNullOrWhiteSpace(request.Subject)
                ? ticketIdSettings.SubjectTemplate
                    .Replace("{ProjectName}", project.Name)
                    .Replace("{TicketId}", displayId)
                    .Replace("{TicketNumber}", ticketNumber.ToString())
                : request.Subject;

            var ticket = new Ticket
            {
                CompanyId = companyId,
                ProjectId = request.ProjectId,
                TicketNumber = ticketNumber,
                Subject = subject,
                Status = TicketStatus.Open,
                Priority = request.Priority,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                Tags = request.Tags ?? new List<string>()
            };

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            ticket.CreatedByUserId = currentUser?.Id;

            var created = await _ticketRepository.CreateAsync(ticket);

            // Create first message if body provided
            if (!string.IsNullOrWhiteSpace(request.Body))
            {
                var message = new TicketMessage
                {
                    TicketId = created.Id,
                    CompanyId = companyId,
                    ProjectId = request.ProjectId,
                    Body = request.Body,
                    BodyHtml = request.BodyHtml,
                    IsInternalNote = false,
                    AuthorUserId = currentUser?.Id,
                    AuthorName = currentUser?.DisplayName,
                    AuthorEmail = currentUser?.Email,
                    Source = MessageSource.Agent
                };
                await _ticketMessageRepository.CreateAsync(message);
            }

            _logger.LogInformation("Ticket created: {TicketId} in project {ProjectId}", created.Id, created.ProjectId);
            await _auditService.LogAsync(companyId, request.ProjectId, "Created", "Ticket", created.Id, _appUser, $"Subject: {subject}");

            // Fire alerts asynchronously (don't block the response)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _alertService.FireAlertsAsync(companyId, request.ProjectId, AlertTriggerType.NewTicket, null, new Dictionary<string, string>
                    {
                        { "ticketId", created.Id },
                        { "subject", subject },
                        { "customerEmail", request.CustomerEmail ?? "" },
                        { "projectName", project.Name }
                    });
                }
                catch { }
            });

            return Ok(_mapper.Map<TicketResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ticket in company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/messages")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketMessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddMessage(string companyId, string id, [FromBody] TicketMessageCommandModel commandModel)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(id);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _ticketGateway.CanAddMessageAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            var message = new TicketMessage
            {
                TicketId = id,
                CompanyId = companyId,
                ProjectId = ticket.ProjectId,
                Body = commandModel.Body,
                BodyHtml = commandModel.BodyHtml,
                IsInternalNote = commandModel.IsInternalNote,
                AuthorUserId = currentUser?.Id,
                AuthorName = currentUser?.DisplayName,
                AuthorEmail = currentUser?.Email,
                Source = MessageSource.Agent
            };

            // If this is a reply (not internal note) and ticket has a customer email, check if SMTP is configured
            if (!commandModel.IsInternalNote && !string.IsNullOrEmpty(ticket.CustomerEmail))
            {
                var replyProject = await _projectRepository.GetAsync(ticket.ProjectId);
                if (replyProject?.Settings?.Smtp != null && !string.IsNullOrEmpty(replyProject.Settings.Smtp.Host))
                {
                    message.SendStatus = MessageSendStatus.Pending;
                }
            }

            var created = await _ticketMessageRepository.CreateAsync(message);

            // Send email asynchronously (don't block the response)
            if (created.SendStatus == MessageSendStatus.Pending)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var project = await _projectRepository.GetAsync(ticket.ProjectId);
                        if (project != null)
                        {
                            // Find the last customer message to quote in the reply
                            var ticketMessages = await _ticketMessageRepository.GetByTicketIdAsync(id);
                            var lastCustomerMsg = ticketMessages
                                .Where(m => m.Source == MessageSource.Customer && m.Id != created.Id)
                                .OrderByDescending(m => m.CreatedOnDateTime)
                                .FirstOrDefault();

                            var (success, error) = await _emailService.SendReplyAsync(
                                project, ticket, created, ticket.CustomerEmail!, ticket.CustomerName, lastCustomerMsg);

                            created.SendStatus = success ? MessageSendStatus.Sent : MessageSendStatus.Failed;
                            created.SendError = error;
                            created.SentOnDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            created.MessageId = $"{created.Id}@zipstation";
                            await _ticketMessageRepository.UpdateAsync(created);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background email send failed for message {MessageId}", created.Id);
                        created.SendStatus = MessageSendStatus.Failed;
                        created.SendError = ex.Message;
                        await _ticketMessageRepository.UpdateAsync(created);
                    }
                });
            }

            // If not an internal note, update ticket status to Pending (awaiting customer reply)
            if (!commandModel.IsInternalNote)
            {
                ticket.LastMessageSource = MessageSource.Agent;
                if (ticket.Status == TicketStatus.Open)
                    ticket.Status = TicketStatus.Pending;
                await _ticketRepository.UpdateAsync(ticket);
            }

            _logger.LogInformation("Message added to ticket {TicketId}", id);
            await _auditService.LogAsync(companyId, ticket.ProjectId, commandModel.IsInternalNote ? "NoteAdded" : "ReplySent", "Ticket", id, _appUser);
            return Ok(_mapper.Map<TicketMessageResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding message to ticket {TicketId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/messages/{messageId}/retry")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketMessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RetryMessage(string companyId, string id, string messageId)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(id);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var messages = await _ticketMessageRepository.GetByTicketIdAsync(id);
            var message = messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null) return NotFound();
            if (message.SendStatus != MessageSendStatus.Failed)
                return BadRequest(new BadRequestResponse { Message = "Only failed messages can be retried" });

            var project = await _projectRepository.GetAsync(ticket.ProjectId);
            if (project == null) return BadRequest(new BadRequestResponse { Message = "Project not found" });

            var (success, error) = await _emailService.SendReplyAsync(
                project, ticket, message, ticket.CustomerEmail!, ticket.CustomerName);

            message.SendStatus = success ? MessageSendStatus.Sent : MessageSendStatus.Failed;
            message.SendError = error;
            message.SentOnDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _ticketMessageRepository.UpdateAsync(message);

            return Ok(_mapper.Map<TicketMessageResponse>(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying message {MessageId}", messageId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/link")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LinkTicket(string companyId, string id, [FromBody] LinkTicketRequest request)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(id);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var targetTicket = await ResolveTicketAsync(companyId, request.TargetTicketId);
            if (targetTicket == null)
                return BadRequest(new BadRequestResponse { Message = "Target ticket not found. Enter a ticket number (e.g. 6) or ticket ID." });

            if (ticket.Id == targetTicket.Id)
                return BadRequest(new BadRequestResponse { Message = "Cannot link a ticket to itself" });

            var gatewayResponse = await _ticketGateway.CanUpdateTicketAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Bidirectional link
            if (!ticket.LinkedTicketIds.Contains(targetTicket.Id))
                ticket.LinkedTicketIds.Add(targetTicket.Id);
            if (!targetTicket.LinkedTicketIds.Contains(ticket.Id))
                targetTicket.LinkedTicketIds.Add(ticket.Id);

            await _ticketRepository.UpdateAsync(ticket);
            await _ticketRepository.UpdateAsync(targetTicket);

            await _auditService.LogAsync(companyId, ticket.ProjectId, "Linked", "Ticket", id, _appUser, $"Linked to {request.TargetTicketId}");
            return Ok(_mapper.Map<TicketResponse>(ticket));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking ticket {TicketId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/unlink")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlinkTicket(string companyId, string id, [FromBody] LinkTicketRequest request)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(id);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var targetTicket = await ResolveTicketAsync(companyId, request.TargetTicketId);

            var gatewayResponse = await _ticketGateway.CanUpdateTicketAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            if (targetTicket != null)
            {
                ticket.LinkedTicketIds.Remove(targetTicket.Id);
                await _ticketRepository.UpdateAsync(ticket);

                targetTicket.LinkedTicketIds.Remove(ticket.Id);
                await _ticketRepository.UpdateAsync(targetTicket);
            }
            else
            {
                ticket.LinkedTicketIds.Remove(request.TargetTicketId);
                await _ticketRepository.UpdateAsync(ticket);
            }

            await _auditService.LogAsync(companyId, ticket.ProjectId, "Unlinked", "Ticket", id, _appUser, $"Unlinked from {request.TargetTicketId}");
            return Ok(_mapper.Map<TicketResponse>(ticket));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking ticket {TicketId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/merge")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MergeTicket(string companyId, string id, [FromBody] MergeTicketRequest request)
    {
        try
        {
            var sourceTicket = await _ticketRepository.GetAsync(id);
            if (sourceTicket == null || sourceTicket.CompanyId != companyId) return NotFound();

            var targetTicket = await ResolveTicketAsync(companyId, request.TargetTicketId);
            if (targetTicket == null)
                return BadRequest(new BadRequestResponse { Message = "Target ticket not found. Enter a ticket number (e.g. 6) or ticket ID." });

            if (sourceTicket.Id == targetTicket.Id)
                return BadRequest(new BadRequestResponse { Message = "Cannot merge a ticket into itself" });

            var gatewayResponse = await _ticketGateway.CanUpdateTicketAsync(companyId, sourceTicket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Move all messages from source to target
            var messages = await _ticketMessageRepository.GetByTicketIdAsync(id);
            foreach (var msg in messages)
            {
                msg.TicketId = targetTicket.Id;
                await _ticketMessageRepository.UpdateAsync(msg);
            }

            // Mark source as merged
            sourceTicket.Status = TicketStatus.Merged;
            sourceTicket.MergedIntoTicketId = targetTicket.Id;
            await _ticketRepository.UpdateAsync(sourceTicket);

            // Add system message to target
            var systemMsg = new TicketMessage
            {
                TicketId = targetTicket.Id,
                CompanyId = companyId,
                ProjectId = targetTicket.ProjectId,
                Body = $"Merged from ticket #{sourceTicket.TicketNumber} ({sourceTicket.Subject}). {messages.Count} messages moved.",
                IsInternalNote = false,
                Source = MessageSource.System
            };
            await _ticketMessageRepository.CreateAsync(systemMsg);

            await _auditService.LogAsync(companyId, sourceTicket.ProjectId, "Merged", "Ticket", id, _appUser, $"Merged into {request.TargetTicketId}");
            return Ok(_mapper.Map<TicketResponse>(targetTicket));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging ticket {TicketId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}/status")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(string companyId, string id, [FromBody] UpdateTicketStatusRequest request)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(id);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _ticketGateway.CanUpdateTicketAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            ticket.Status = request.Status;
            var updated = await _ticketRepository.UpdateAsync(ticket);

            var statusUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            var statusUserName = statusUser?.DisplayName ?? "Someone";
            var systemMsg = new TicketMessage
            {
                TicketId = id,
                CompanyId = companyId,
                ProjectId = ticket.ProjectId,
                Body = $"{statusUserName} changed status to {request.Status}.",
                IsInternalNote = false,
                Source = MessageSource.System
            };
            await _ticketMessageRepository.CreateAsync(systemMsg);

            _logger.LogInformation("Ticket {TicketId} status changed to {Status}", id, request.Status);
            await _auditService.LogAsync(companyId, ticket.ProjectId, "StatusChanged", "Ticket", id, _appUser, $"Status: {request.Status}");
            return Ok(_mapper.Map<TicketResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ticket status {TicketId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}/priority")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePriority(string companyId, string id, [FromBody] UpdateTicketPriorityRequest request)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(id);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _ticketGateway.CanUpdateTicketAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var oldPriority = ticket.Priority;
            ticket.Priority = request.Priority;
            var updated = await _ticketRepository.UpdateAsync(ticket);

            var priorityUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            var priorityUserName = priorityUser?.DisplayName ?? "Someone";
            var systemMsg = new TicketMessage
            {
                TicketId = id,
                CompanyId = companyId,
                ProjectId = ticket.ProjectId,
                Body = $"{priorityUserName} changed priority from {oldPriority} to {request.Priority}.",
                IsInternalNote = false,
                Source = MessageSource.System
            };
            await _ticketMessageRepository.CreateAsync(systemMsg);

            await _auditService.LogAsync(companyId, ticket.ProjectId, "PriorityChanged", "Ticket", id, _appUser, $"Priority: {request.Priority}");
            return Ok(_mapper.Map<TicketResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ticket priority {TicketId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}/assign")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignTicket(string companyId, string id, [FromBody] AssignTicketRequest request)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(id);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _ticketGateway.CanUpdateTicketAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            ticket.AssignedToUserId = request.UserId;
            var updated = await _ticketRepository.UpdateAsync(ticket);

            _logger.LogInformation("Ticket {TicketId} assigned to {UserId}", id, request.UserId);
            await _auditService.LogAsync(companyId, ticket.ProjectId, "Assigned", "Ticket", id, _appUser, $"Assigned to: {request.UserId ?? "Unassigned"}");
            return Ok(_mapper.Map<TicketResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning ticket {TicketId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}/tags")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTags(string companyId, string id, [FromBody] UpdateTagsRequest request)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(id);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _ticketGateway.CanUpdateTicketAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            ticket.Tags = request.Tags ?? new List<string>();
            var updated = await _ticketRepository.UpdateAsync(ticket);

            _logger.LogInformation("Ticket {TicketId} tags updated", id);
            await _auditService.LogAsync(companyId, ticket.ProjectId, "TagsUpdated", "Ticket", id, _appUser, $"Tags: {string.Join(", ", ticket.Tags)}");
            return Ok(_mapper.Map<TicketResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tags for ticket {TicketId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("bulk")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkAction(string companyId, [FromBody] BulkTicketActionRequest request)
    {
        try
        {
            if (request.TicketIds == null || request.TicketIds.Count == 0)
                return BadRequest(new BadRequestResponse { Message = "No ticket IDs provided" });

            var validActions = new[] { "close", "resolve", "assign", "delete" };
            if (!validActions.Contains(request.Action?.ToLower()))
                return BadRequest(new BadRequestResponse { Message = $"Invalid action. Must be one of: {string.Join(", ", validActions)}" });

            var gatewayResponse = await _ticketGateway.CanListTicketsAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var affectedCount = 0;

            foreach (var ticketId in request.TicketIds)
            {
                var ticket = await _ticketRepository.GetAsync(ticketId);
                if (ticket == null || ticket.CompanyId != companyId)
                    continue;

                switch (request.Action.ToLower())
                {
                    case "close":
                        ticket.Status = TicketStatus.Closed;
                        await _ticketRepository.UpdateAsync(ticket);
                        await _auditService.LogAsync(companyId, ticket.ProjectId, "StatusChanged", "Ticket", ticketId, _appUser, "Status: Closed (bulk)");
                        break;

                    case "resolve":
                        ticket.Status = TicketStatus.Resolved;
                        await _ticketRepository.UpdateAsync(ticket);
                        await _auditService.LogAsync(companyId, ticket.ProjectId, "StatusChanged", "Ticket", ticketId, _appUser, "Status: Resolved (bulk)");
                        break;

                    case "assign":
                        ticket.AssignedToUserId = request.AssignToUserId;
                        await _ticketRepository.UpdateAsync(ticket);
                        await _auditService.LogAsync(companyId, ticket.ProjectId, "Assigned", "Ticket", ticketId, _appUser, $"Assigned to: {request.AssignToUserId ?? "Unassigned"} (bulk)");
                        break;

                    case "delete":
                        ticket.IsVoid = true;
                        await _ticketRepository.UpdateAsync(ticket);
                        await _auditService.LogAsync(companyId, ticket.ProjectId, "Deleted", "Ticket", ticketId, _appUser, "Soft-deleted (bulk)");
                        break;
                }

                affectedCount++;
            }

            _logger.LogInformation("Bulk action '{Action}' performed on {Count} tickets in company {CompanyId}", request.Action, affectedCount, companyId);
            return Ok(new { affectedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing bulk action on tickets in company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("stats")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetStats(string companyId, [FromQuery] string? projectId = null)
    {
        try
        {
            var gatewayResponse = await _ticketGateway.CanListTicketsAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var open = await CountByStatusFilteredAsync(companyId, projectId, TicketStatus.Open);
            var pending = await CountByStatusFilteredAsync(companyId, projectId, TicketStatus.Pending);
            var resolved = await CountByStatusFilteredAsync(companyId, projectId, TicketStatus.Resolved);
            var closed = await CountByStatusFilteredAsync(companyId, projectId, TicketStatus.Closed);

            // Get capacity warnings per project
            var projects = await _projectRepository.GetByCompanyIdAsync(companyId);
            var capacityWarnings = new List<object>();

            foreach (var proj in projects)
            {
                var settings = proj.Settings?.TicketId ?? new TicketIdSettings();
                if (settings.Format == TicketIdFormat.Numeric)
                {
                    var maxCapacity = (long)Math.Pow(10, settings.MaxLength);
                    var currentCount = await _ticketIdCounterRepository.GetCurrentValueAsync(proj.Id);
                    var remaining = maxCapacity - currentCount;
                    var usagePercent = maxCapacity > 0 ? (double)currentCount / maxCapacity * 100 : 0;

                    if (usagePercent >= 80)
                    {
                        capacityWarnings.Add(new
                        {
                            projectId = proj.Id,
                            projectName = proj.Name,
                            currentCount,
                            maxCapacity,
                            remaining,
                            usagePercent = Math.Round(usagePercent, 1)
                        });
                    }
                }
            }

            return Ok(new
            {
                open,
                pending,
                resolved,
                closed,
                total = open + pending + resolved + closed,
                capacityWarnings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ticket stats for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("response-times")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetResponseTimes(string companyId, [FromQuery] string? projectId = null)
    {
        try
        {
            var gatewayResponse = await _ticketGateway.CanListTicketsAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Get all tickets for the company/project
            var filter = Builders<Ticket>.Filter.Eq(t => t.CompanyId, companyId)
                       & Builders<Ticket>.Filter.Eq(t => t.IsVoid, false);
            if (!string.IsNullOrEmpty(projectId))
                filter &= Builders<Ticket>.Filter.Eq(t => t.ProjectId, projectId);

            var tickets = await _ticketRepository.GetCollection().Find(filter)
                .SortByDescending(t => t.CreatedOnDateTime)
                .Limit(100) // Last 100 tickets for performance
                .ToListAsync();

            // Calculate average response time per agent
            var agentStats = new Dictionary<string, (int count, double totalMinutes, string name)>();
            var responseTimes = new List<double>();

            foreach (var ticket in tickets)
            {
                if (string.IsNullOrEmpty(ticket.AssignedToUserId)) continue;

                var messages = await _ticketMessageRepository.GetByTicketIdAsync(ticket.Id);
                var customerMsg = messages.FirstOrDefault(m => m.Source == MessageSource.Customer);
                var agentMsg = messages.FirstOrDefault(m => m.Source == MessageSource.Agent && m.CreatedOnDateTime > (customerMsg?.CreatedOnDateTime ?? 0));

                if (customerMsg != null && agentMsg != null)
                {
                    var responseMinutes = (agentMsg.CreatedOnDateTime - customerMsg.CreatedOnDateTime) / 60000.0;
                    responseTimes.Add(responseMinutes);

                    var agentId = ticket.AssignedToUserId;
                    if (!agentStats.ContainsKey(agentId))
                        agentStats[agentId] = (0, 0, "");
                    var (count, total, name) = agentStats[agentId];
                    agentStats[agentId] = (count + 1, total + responseMinutes, agentMsg.AuthorName ?? agentId);
                }
            }

            var avgResponseMinutes = responseTimes.Count > 0 ? responseTimes.Average() : 0;
            var medianResponseMinutes = responseTimes.Count > 0
                ? responseTimes.OrderBy(x => x).ElementAt(responseTimes.Count / 2)
                : 0;

            var agents = agentStats.Select(kvp => new {
                agentId = kvp.Key,
                agentName = kvp.Value.name,
                ticketsHandled = kvp.Value.count,
                avgResponseMinutes = kvp.Value.count > 0 ? Math.Round(kvp.Value.totalMinutes / kvp.Value.count, 1) : 0
            }).OrderBy(a => a.avgResponseMinutes).ToList();

            return Ok(new {
                avgResponseMinutes = Math.Round(avgResponseMinutes, 1),
                medianResponseMinutes = Math.Round(medianResponseMinutes, 1),
                totalTicketsAnalyzed = tickets.Count,
                totalWithResponses = responseTimes.Count,
                agents
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing response times for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/heartbeat")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Heartbeat(string companyId, string id)
    {
        var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
        if (currentUser == null) return Unauthorized();

        var collection = _database.GetCollection<TicketViewer>("ticketViewers");
        var filter = Builders<TicketViewer>.Filter.Eq(v => v.TicketId, id)
                   & Builders<TicketViewer>.Filter.Eq(v => v.UserId, currentUser.Id);

        var update = Builders<TicketViewer>.Update
            .Set(v => v.LastHeartbeat, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .Set(v => v.UserDisplayName, currentUser.DisplayName)
            .Set(v => v.AvatarUrl, currentUser.AvatarUrl)
            .SetOnInsert(v => v.Id, MongoDB.Bson.ObjectId.GenerateNewId().ToString())
            .SetOnInsert(v => v.TicketId, id)
            .SetOnInsert(v => v.UserId, currentUser.Id);

        await collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });

        // Return current viewers (active in last 60 seconds, excluding self)
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-60).ToUnixTimeMilliseconds();
        var viewers = await collection
            .Find(Builders<TicketViewer>.Filter.Eq(v => v.TicketId, id)
                & Builders<TicketViewer>.Filter.Gt(v => v.LastHeartbeat, cutoff)
                & Builders<TicketViewer>.Filter.Ne(v => v.UserId, currentUser.Id))
            .ToListAsync();

        return Ok(viewers.Select(v => new { v.UserId, v.UserDisplayName, v.AvatarUrl }));
    }

    private async Task<long> CountByStatusFilteredAsync(string companyId, string? projectId, TicketStatus status)
    {
        if (string.IsNullOrEmpty(projectId))
            return await _ticketRepository.CountByStatusAsync(companyId, status);

        var filter = Builders<Ticket>.Filter.Eq(t => t.CompanyId, companyId)
                   & Builders<Ticket>.Filter.Eq(t => t.ProjectId, projectId)
                   & Builders<Ticket>.Filter.Eq(t => t.Status, status)
                   & Builders<Ticket>.Filter.Eq(t => t.IsVoid, false);
        return await _ticketRepository.GetCollection().CountDocumentsAsync(filter);
    }

    private async Task<Ticket?> ResolveTicketAsync(string companyId, string targetIdOrNumber)
    {
        // Try by MongoDB ID first
        var ticket = await _ticketRepository.GetAsync(targetIdOrNumber);
        if (ticket != null && ticket.CompanyId == companyId) return ticket;

        // Try by ticket number (strip leading zeros, parse as number)
        if (long.TryParse(targetIdOrNumber.TrimStart('0'), out var ticketNum) && ticketNum > 0)
        {
            ticket = await _ticketRepository.GetByTicketNumberAsync(companyId, ticketNum);
            if (ticket != null) return ticket;
        }

        return null;
    }

    private async Task<long> GenerateTicketNumberAsync(string projectId, TicketIdSettings settings)
    {
        if (!settings.UseRandomNumbers)
        {
            var next = await _ticketIdCounterRepository.GetNextTicketNumberAsync(projectId);
            return Math.Max(next, settings.StartingNumber > 0 ? settings.StartingNumber : next);
        }

        // Random number generation within the configured range
        var min = settings.StartingNumber > 0 ? settings.StartingNumber : (long)Math.Pow(10, Math.Max(settings.MinLength, 3) - 1);
        var max = (long)Math.Pow(10, settings.MaxLength) - 1;
        if (min > max) min = max / 2;

        var random = new Random();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var candidate = (long)(random.NextDouble() * (max - min + 1)) + min;
            var exists = await _ticketRepository.ExistsByTicketNumberAndProjectAsync(projectId, candidate);
            if (!exists) return candidate;
        }

        // Fallback: use sequential if random fails after 100 attempts
        return await _ticketIdCounterRepository.GetNextTicketNumberAsync(projectId);
    }

    private static string GenerateDisplayId(long ticketNumber, TicketIdSettings settings)
    {
        var prefix = settings.Prefix ?? "";
        var minLen = Math.Max(settings.MinLength, 3);

        string idPart;
        switch (settings.Format)
        {
            case TicketIdFormat.Alphanumeric:
                idPart = ToAlphanumeric(ticketNumber).PadLeft(minLen, '0');
                break;

            case TicketIdFormat.DateNumeric:
                var datePart = DateTime.UtcNow.ToString("yyMMdd");
                var numPart = ticketNumber.ToString().PadLeft(Math.Max(minLen - 6, 2), '0');
                idPart = $"{datePart}-{numPart}";
                break;

            default: // Numeric
                idPart = ticketNumber.ToString().PadLeft(minLen, '0');
                break;
        }

        return string.IsNullOrEmpty(prefix) ? idPart : $"{prefix}-{idPart}";
    }

    [HttpGet("{id}/draft")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetDraft(string companyId, string id)
    {
        var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
        if (currentUser == null) return Unauthorized();

        var draft = await _ticketDraftRepository.GetByTicketAndUserAsync(id, currentUser.Id);
        if (draft == null) return Ok(new { exists = false });

        return Ok(new { exists = true, body = draft.Body, bodyHtml = draft.BodyHtml, isInternalNote = draft.IsInternalNote });
    }

    [HttpPut("{id}/draft")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> SaveDraft(string companyId, string id, [FromBody] SaveDraftRequest request)
    {
        var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
        if (currentUser == null) return Unauthorized();

        var draft = await _ticketDraftRepository.GetByTicketAndUserAsync(id, currentUser.Id);
        if (draft == null)
        {
            draft = new TicketDraft
            {
                TicketId = id,
                UserId = currentUser.Id,
                Body = request.Body,
                BodyHtml = request.BodyHtml,
                IsInternalNote = request.IsInternalNote
            };
            await _ticketDraftRepository.CreateAsync(draft);
        }
        else
        {
            draft.Body = request.Body;
            draft.BodyHtml = request.BodyHtml;
            draft.IsInternalNote = request.IsInternalNote;
            await _ticketDraftRepository.UpdateAsync(draft);
        }

        return Ok();
    }

    [HttpDelete("{id}/draft")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> DeleteDraft(string companyId, string id)
    {
        var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
        if (currentUser == null) return Unauthorized();

        var draft = await _ticketDraftRepository.GetByTicketAndUserAsync(id, currentUser.Id);
        if (draft != null)
            await _ticketDraftRepository.RemoveAsync(draft.Id);

        return Ok();
    }

    private static string ToAlphanumeric(long number)
    {
        const string chars = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // no I, O (ambiguous)
        if (number == 0) return "0";

        var result = "";
        while (number > 0)
        {
            result = chars[(int)(number % chars.Length)] + result;
            number /= chars.Length;
        }
        return result;
    }
}

public class CreateTicketRequest
{
    public string ProjectId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public List<string>? Tags { get; set; }
    public string? Body { get; set; }
    public string? BodyHtml { get; set; }
}

public class UpdateTicketStatusRequest
{
    public TicketStatus Status { get; set; }
}

public class LinkTicketRequest
{
    public string TargetTicketId { get; set; } = string.Empty;
}

public class MergeTicketRequest
{
    public string TargetTicketId { get; set; } = string.Empty;
}

public class AssignTicketRequest
{
    public string? UserId { get; set; }
}

public class SaveDraftRequest
{
    public string Body { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    public bool IsInternalNote { get; set; }
}

public class UpdateTagsRequest
{
    public List<string> Tags { get; set; } = new();
}

public class UpdateTicketPriorityRequest
{
    public TicketPriority Priority { get; set; }
}

public class BulkTicketActionRequest
{
    public List<string> TicketIds { get; set; } = new();
    public string Action { get; set; } = string.Empty; // "close", "resolve", "assign", "delete"
    public string? AssignToUserId { get; set; }
}
