using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;
using ZipStation.Models.Responses;
using ZipStation.Models.SearchProfiles;
using ZipStation.Business.Services;
using MongoDB.Driver;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/intake")]
[Authorize]
public class IntakeController : BaseController
{
    private readonly ILogger<IntakeController> _logger;
    private readonly IIntakeEmailRepository _intakeEmailRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketMessageRepository _ticketMessageRepository;
    private readonly ITicketIdCounterRepository _ticketIdCounterRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IAppUser _appUser;
    private readonly IIntakeGateway _intakeGateway;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;
    private readonly IAlertService _alertService;

    public IntakeController(
        ILogger<IntakeController> logger,
        IIntakeEmailRepository intakeEmailRepository,
        ITicketRepository ticketRepository,
        ITicketMessageRepository ticketMessageRepository,
        ITicketIdCounterRepository ticketIdCounterRepository,
        ICustomerRepository customerRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IMapper mapper,
        IAppUser appUser,
        IIntakeGateway intakeGateway,
        IAuditService auditService,
        IEmailService emailService,
        IAlertService alertService)
    {
        _logger = logger;
        _intakeEmailRepository = intakeEmailRepository;
        _ticketRepository = ticketRepository;
        _ticketMessageRepository = ticketMessageRepository;
        _ticketIdCounterRepository = ticketIdCounterRepository;
        _customerRepository = customerRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _appUser = appUser;
        _intakeGateway = intakeGateway;
        _auditService = auditService;
        _emailService = emailService;
        _alertService = alertService;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PaginatedResponse<IntakeEmailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListIntake(
        string companyId,
        [FromQuery] string? projectId = null,
        [FromQuery] IntakeStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int resultsPerPage = 25)
    {
        try
        {
            var gatewayResponse = await _intakeGateway.CanListIntakeAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var filter = Builders<IntakeEmail>.Filter.Eq(e => e.CompanyId, companyId);

            if (!string.IsNullOrEmpty(projectId))
                filter &= Builders<IntakeEmail>.Filter.Eq(e => e.ProjectId, projectId);

            if (status.HasValue)
                filter &= Builders<IntakeEmail>.Filter.Eq(e => e.Status, status.Value);

            var searchProfile = new BaseSearchProfile
            {
                Page = page,
                ResultsPerPage = resultsPerPage,
                OrderByFieldName = "createdOnDateTime",
                OrderByAscending = false
            };

            var result = await _intakeEmailRepository.GetPaginatedResults(filter, searchProfile);

            return Ok(new PaginatedResponse<IntakeEmailResponse>
            {
                TotalResultCount = result.TotalResultCount,
                Results = _mapper.Map<List<IntakeEmailResponse>>(result.Results)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing intake for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IntakeEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIntake(string companyId, string id)
    {
        try
        {
            var intake = await _intakeEmailRepository.GetAsync(id);
            if (intake == null || intake.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _intakeGateway.CanGetIntakeAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            return Ok(_mapper.Map<IntakeEmailResponse>(intake));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting intake {IntakeId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/approve")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveIntake(string companyId, string id)
    {
        try
        {
            var intake = await _intakeEmailRepository.GetAsync(id);
            if (intake == null || intake.CompanyId != companyId) return NotFound();

            if (intake.Status != IntakeStatus.Pending)
                return BadRequest(new BadRequestResponse { Message = "This intake email has already been processed" });

            var gatewayResponse = await _intakeGateway.CanApproveIntakeAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            // Get or create customer
            var customer = await _customerRepository.GetByEmailAndProjectAsync(intake.FromEmail, intake.ProjectId);
            if (customer == null)
            {
                customer = new Customer
                {
                    CompanyId = companyId,
                    ProjectId = intake.ProjectId,
                    Email = intake.FromEmail,
                    Name = intake.FromName
                };
                await _customerRepository.CreateAsync(customer);
            }

            // Generate ticket number and subject from template
            var project = await _projectRepository.GetAsync(intake.ProjectId);
            var ticketIdSettings = project?.Settings?.TicketId ?? new TicketIdSettings();
            var ticketNumber = await GenerateTicketNumberAsync(intake.ProjectId, ticketIdSettings);
            var minLen = Math.Max(ticketIdSettings.MinLength, 3);
            var displayId = ticketNumber.ToString().PadLeft(minLen, '0');
            if (!string.IsNullOrEmpty(ticketIdSettings.Prefix))
                displayId = $"{ticketIdSettings.Prefix}-{displayId}";
            var subject = (ticketIdSettings.SubjectTemplate ?? "{ProjectName} - Ticket {TicketId}")
                .Replace("{ProjectName}", project?.Name ?? "Support")
                .Replace("{TicketId}", displayId)
                .Replace("{TicketNumber}", ticketNumber.ToString());

            // Create ticket
            var ticket = new Ticket
            {
                CompanyId = companyId,
                ProjectId = intake.ProjectId,
                TicketNumber = ticketNumber,
                Subject = subject,
                Status = TicketStatus.Open,
                Priority = TicketPriority.Normal,
                CustomerName = intake.FromName,
                CustomerEmail = intake.FromEmail,
                CreationSource = TicketCreationSource.IntakeManual,
                CreatedByUserId = currentUser?.Id
            };
            var createdTicket = await _ticketRepository.CreateAsync(ticket);

            // Create first message from customer
            var message = new TicketMessage
            {
                TicketId = createdTicket.Id,
                CompanyId = companyId,
                ProjectId = intake.ProjectId,
                Body = intake.BodyText,
                BodyHtml = intake.BodyHtml,
                IsInternalNote = false,
                AuthorName = intake.FromName,
                AuthorEmail = intake.FromEmail,
                Source = MessageSource.Customer
            };
            await _ticketMessageRepository.CreateAsync(message);

            // Update intake status
            intake.Status = IntakeStatus.Approved;
            intake.ApprovedByUserId = currentUser?.Id;
            intake.ProcessedOn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            intake.TicketId = createdTicket.Id;
            await _intakeEmailRepository.UpdateAsync(intake);

            // Update customer ticket count
            customer.TotalTicketCount++;
            customer.OpenTicketCount++;
            await _customerRepository.UpdateAsync(customer);

            _logger.LogInformation("Intake {IntakeId} approved, ticket {TicketId} created", id, createdTicket.Id);
            await _auditService.LogAsync(companyId, intake.ProjectId, "Approved", "IntakeEmail", id, _appUser, $"Created ticket {createdTicket.Id}");

            // Fire alerts
            _ = Task.Run(async () => {
                try { await _alertService.FireAlertsAsync(companyId, intake.ProjectId, Models.Entities.AlertTriggerType.NewTicket, null, new Dictionary<string, string> {
                    { "ticketId", createdTicket.Id }, { "subject", subject }, { "customerEmail", createdTicket.CustomerEmail ?? "" }, { "projectName", project?.Name ?? "" }
                }); } catch { }
            });

            // Send auto-reply if configured AND enabled
            if (project != null && !string.IsNullOrEmpty(createdTicket.CustomerEmail)
                && project.Settings?.AutoReply != null && project.Settings.AutoReply.Enabled)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendAutoReplyAsync(project, createdTicket, createdTicket.CustomerEmail!, createdTicket.CustomerName);

                        var systemMsg = new TicketMessage
                        {
                            TicketId = createdTicket.Id,
                            CompanyId = companyId,
                            ProjectId = intake.ProjectId,
                            Body = "Auto-reply sent to customer.",
                            IsInternalNote = false,
                            Source = MessageSource.System
                        };
                        await _ticketMessageRepository.CreateAsync(systemMsg);
                    }
                    catch (Exception ex) { _logger.LogError(ex, "Auto-reply failed for ticket {TicketId}", createdTicket.Id); }
                });
            }

            return Ok(_mapper.Map<TicketResponse>(createdTicket));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving intake {IntakeId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("bulk")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> BulkAction(string companyId, [FromBody] BulkIntakeActionRequest request)
    {
        try
        {
            var gatewayResponse = await _intakeGateway.CanApproveIntakeAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var affectedCount = 0;
            foreach (var intakeId in request.IntakeIds)
            {
                try
                {
                    var intake = await _intakeEmailRepository.GetAsync(intakeId);
                    if (intake == null || intake.CompanyId != companyId || intake.Status != IntakeStatus.Pending) continue;

                    switch (request.Action)
                    {
                        case "approve":
                            // Simplified approve — create ticket
                            var project = await _projectRepository.GetAsync(intake.ProjectId);
                            if (project == null) continue;
                            var customer = await _customerRepository.GetByEmailAndProjectAsync(intake.FromEmail, intake.ProjectId);
                            if (customer == null)
                            {
                                customer = new Customer { CompanyId = companyId, ProjectId = intake.ProjectId, Email = intake.FromEmail, Name = intake.FromName };
                                await _customerRepository.CreateAsync(customer);
                            }
                            var ticketIdSettings = project.Settings?.TicketId ?? new TicketIdSettings();
                            var ticketNumber = await GenerateTicketNumberAsync(intake.ProjectId, ticketIdSettings);
                            var minLen = Math.Max(ticketIdSettings.MinLength, 3);
                            var displayId = ticketNumber.ToString().PadLeft(minLen, '0');
                            if (!string.IsNullOrEmpty(ticketIdSettings.Prefix)) displayId = $"{ticketIdSettings.Prefix}-{displayId}";
                            var subject = (ticketIdSettings.SubjectTemplate ?? "{ProjectName} - Ticket {TicketId}")
                                .Replace("{ProjectName}", project.Name).Replace("{TicketId}", displayId).Replace("{TicketNumber}", ticketNumber.ToString());
                            var ticket = new Ticket
                            {
                                CompanyId = companyId, ProjectId = intake.ProjectId, TicketNumber = ticketNumber,
                                Subject = subject, Status = TicketStatus.Open, Priority = TicketPriority.Normal,
                                CustomerName = intake.FromName, CustomerEmail = intake.FromEmail,
                                CreationSource = TicketCreationSource.IntakeManual
                            };
                            await _ticketRepository.CreateAsync(ticket);
                            var msg = new TicketMessage
                            {
                                TicketId = ticket.Id, CompanyId = companyId, ProjectId = intake.ProjectId,
                                Body = intake.BodyText, BodyHtml = intake.BodyHtml, IsInternalNote = false,
                                AuthorName = intake.FromName, AuthorEmail = intake.FromEmail, Source = MessageSource.Customer
                            };
                            await _ticketMessageRepository.CreateAsync(msg);
                            intake.Status = IntakeStatus.Approved;
                            intake.TicketId = ticket.Id;
                            intake.ProcessedOn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            await _intakeEmailRepository.UpdateAsync(intake);
                            customer.TotalTicketCount++; customer.OpenTicketCount++;
                            await _customerRepository.UpdateAsync(customer);
                            break;

                        case "deny":
                            intake.Status = IntakeStatus.Denied;
                            intake.ProcessedOn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            await _intakeEmailRepository.UpdateAsync(intake);
                            break;

                        case "delete":
                            await _intakeEmailRepository.RemoveAsync(intakeId);
                            break;

                        default: continue;
                    }
                    affectedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bulk intake action failed for {IntakeId}", intakeId);
                }
            }

            return Ok(new { affectedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk intake action");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/deny")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IntakeEmailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DenyIntake(string companyId, string id, [FromQuery] bool permanent = false)
    {
        try
        {
            var intake = await _intakeEmailRepository.GetAsync(id);
            if (intake == null || intake.CompanyId != companyId) return NotFound();

            if (intake.Status != IntakeStatus.Pending)
                return BadRequest(new BadRequestResponse { Message = "This intake email has already been processed" });

            var gatewayResponse = await _intakeGateway.CanDenyIntakeAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            intake.Status = IntakeStatus.Denied;
            intake.DeniedByUserId = currentUser?.Id;
            intake.DeniedPermanently = permanent;
            intake.ProcessedOn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _intakeEmailRepository.UpdateAsync(intake);

            _logger.LogInformation("Intake {IntakeId} denied (permanent: {Permanent})", id, permanent);
            await _auditService.LogAsync(companyId, intake.ProjectId, permanent ? "DeniedPermanently" : "Denied", "IntakeEmail", id, _appUser);
            return Ok(_mapper.Map<IntakeEmailResponse>(intake));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying intake {IntakeId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    private async Task<long> GenerateTicketNumberAsync(string projectId, TicketIdSettings settings)
    {
        if (!settings.UseRandomNumbers)
        {
            var next = await _ticketIdCounterRepository.GetNextTicketNumberAsync(projectId);
            return Math.Max(next, settings.StartingNumber > 0 ? settings.StartingNumber : next);
        }

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

        return await _ticketIdCounterRepository.GetNextTicketNumberAsync(projectId);
    }
}

public class BulkIntakeActionRequest
{
    public List<string> IntakeIds { get; set; } = new();
    public string Action { get; set; } = string.Empty; // "approve", "deny", "delete"
}
