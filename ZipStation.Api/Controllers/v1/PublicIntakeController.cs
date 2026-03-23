using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/intake")]
public class PublicIntakeController : BaseController
{
    private readonly ILogger<PublicIntakeController> _logger;
    private readonly IProjectApiKeyRepository _apiKeyRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IIntakeEmailRepository _intakeEmailRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketMessageRepository _ticketMessageRepository;
    private readonly ITicketIdCounterRepository _ticketIdCounterRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMapper _mapper;
    private readonly IAlertService _alertService;
    private readonly IEmailService _emailService;

    public PublicIntakeController(
        ILogger<PublicIntakeController> logger,
        IProjectApiKeyRepository apiKeyRepository,
        IProjectRepository projectRepository,
        IIntakeEmailRepository intakeEmailRepository,
        ITicketRepository ticketRepository,
        ITicketMessageRepository ticketMessageRepository,
        ITicketIdCounterRepository ticketIdCounterRepository,
        ICustomerRepository customerRepository,
        IMapper mapper,
        IAlertService alertService,
        IEmailService emailService)
    {
        _logger = logger;
        _apiKeyRepository = apiKeyRepository;
        _projectRepository = projectRepository;
        _intakeEmailRepository = intakeEmailRepository;
        _ticketRepository = ticketRepository;
        _ticketMessageRepository = ticketMessageRepository;
        _ticketIdCounterRepository = ticketIdCounterRepository;
        _customerRepository = customerRepository;
        _mapper = mapper;
        _alertService = alertService;
        _emailService = emailService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PublicIntakeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitTicket([FromBody] PublicIntakeRequest request)
    {
        // Validate API key from header
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
            return Unauthorized(new { message = "Missing X-Api-Key header" });

        var keyHash = HashKey(apiKey);
        var keyRecord = await _apiKeyRepository.GetByKeyHashAsync(keyHash);
        if (keyRecord == null)
            return Unauthorized(new { message = "Invalid API key" });

        var project = await _projectRepository.GetAsync(keyRecord.ProjectId);
        if (project == null)
            return BadRequest(new { message = "Project not found" });

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required" });
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Message is required" });

        // Get or create customer
        var customer = await _customerRepository.GetByEmailAndProjectAsync(request.Email, project.Id);
        if (customer == null)
        {
            customer = new Customer
            {
                CompanyId = project.CompanyId,
                ProjectId = project.Id,
                Email = request.Email,
                Name = request.Name ?? request.Email.Split('@')[0]
            };
            await _customerRepository.CreateAsync(customer);
        }

        // Check for existing open ticket from this customer (thread onto it)
        var existingTicket = await _ticketRepository.GetByCustomerEmailAndProjectAsync(request.Email, project.Id);
        if (existingTicket != null && !request.ForceNewTicket)
        {
            // Thread onto existing ticket
            var message = new TicketMessage
            {
                TicketId = existingTicket.Id,
                CompanyId = project.CompanyId,
                ProjectId = project.Id,
                Body = request.Message,
                BodyHtml = request.MessageHtml,
                IsInternalNote = false,
                AuthorName = request.Name ?? customer.Name,
                AuthorEmail = request.Email,
                Source = MessageSource.Customer
            };
            await _ticketMessageRepository.CreateAsync(message);

            _logger.LogInformation("Public API: threaded message onto ticket {TicketId} from {Email}", existingTicket.Id, request.Email);

            return Ok(new PublicIntakeResponse
            {
                TicketId = existingTicket.Id,
                Subject = existingTicket.Subject,
                IsNewTicket = false
            });
        }

        // Create new ticket
        var ticketNumber = await _ticketIdCounterRepository.GetNextTicketNumberAsync(project.Id);
        var ticketIdSettings = project.Settings?.TicketId ?? new TicketIdSettings();
        var minLen = Math.Max(ticketIdSettings.MinLength, 3);
        var displayId = ticketNumber.ToString().PadLeft(minLen, '0');
        if (!string.IsNullOrEmpty(ticketIdSettings.Prefix))
            displayId = $"{ticketIdSettings.Prefix}-{displayId}";

        var subject = request.Subject;
        if (string.IsNullOrWhiteSpace(subject))
        {
            subject = (ticketIdSettings.SubjectTemplate ?? "{ProjectName} - Ticket {TicketId}")
                .Replace("{ProjectName}", project.Name)
                .Replace("{TicketId}", displayId)
                .Replace("{TicketNumber}", ticketNumber.ToString());
        }

        var ticket = new Ticket
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            TicketNumber = ticketNumber,
            Subject = subject,
            Status = TicketStatus.Open,
            Priority = TicketPriority.Normal,
            CustomerName = request.Name,
            CustomerEmail = request.Email,
            Tags = request.Tags ?? new List<string>(),
            CreationSource = TicketCreationSource.DirectApi
        };

        var created = await _ticketRepository.CreateAsync(ticket);

        var ticketMessage = new TicketMessage
        {
            TicketId = created.Id,
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            Body = request.Message,
            BodyHtml = request.MessageHtml,
            IsInternalNote = false,
            AuthorName = request.Name ?? customer.Name,
            AuthorEmail = request.Email,
            Source = MessageSource.Customer
        };
        await _ticketMessageRepository.CreateAsync(ticketMessage);

        // Update customer counts
        customer.TotalTicketCount++;
        customer.OpenTicketCount++;
        await _customerRepository.UpdateAsync(customer);

        _logger.LogInformation("Public API: created ticket {TicketId} from {Email}", created.Id, request.Email);

        // Fire alerts + auto-reply in background
        _ = Task.Run(async () => {
            try {
                await _alertService.FireAlertsAsync(project.CompanyId, project.Id, AlertTriggerType.NewTicket, null, new Dictionary<string, string> {
                    { "ticketId", created.Id }, { "subject", subject }, { "customerEmail", request.Email }, { "projectName", project.Name }
                });
            } catch { }
            try {
                await _emailService.SendAutoReplyAsync(project, created, request.Email, request.Name);
            } catch { }
        });

        return Ok(new PublicIntakeResponse
        {
            TicketId = created.Id,
            Subject = subject,
            IsNewTicket = true
        });
    }

    private static string HashKey(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(bytes);
    }
}

public class PublicIntakeRequest
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? MessageHtml { get; set; }
    public List<string>? Tags { get; set; }
    public bool ForceNewTicket { get; set; }
}

public class PublicIntakeResponse
{
    public string TicketId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public bool IsNewTicket { get; set; }
}
