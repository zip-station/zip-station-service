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
using ZipStation.Business.Services;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/projects")]
[Authorize]
public class ProjectsController : BaseController
{
    private readonly ILogger<ProjectsController> _logger;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IProjectGateway _projectGateway;
    private readonly IAppUser _appUser;
    private readonly IAuditService _auditService;
    private readonly ITicketIdCounterRepository _ticketIdCounterRepository;

    public ProjectsController(
        ILogger<ProjectsController> logger,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IMapper mapper,
        IProjectGateway projectGateway,
        IAppUser appUser,
        IAuditService auditService,
        ITicketIdCounterRepository ticketIdCounterRepository)
    {
        _logger = logger;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _projectGateway = projectGateway;
        _appUser = appUser;
        _auditService = auditService;
        _ticketIdCounterRepository = ticketIdCounterRepository;
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProject(string companyId, [FromBody] ProjectCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _projectGateway.CanCreateProjectAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existingSlug = await _projectRepository.GetBySlugAsync(companyId, commandModel.Slug);
            if (existingSlug != null)
                return BadRequest(new BadRequestResponse { Message = "A project with this slug already exists in this company" });

            var project = _mapper.Map<Project>(commandModel);
            project.CompanyId = companyId;

            var created = await _projectRepository.CreateAsync(project);

            _logger.LogInformation("Project created: {ProjectId} in company {CompanyId}", created.Id, companyId);
            await _auditService.LogAsync(companyId, created.Id, "Created", "Project", created.Id, _appUser, $"Name: {created.Name}");
            return Ok(_mapper.Map<ProjectResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project in company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProject(string companyId, string id)
    {
        try
        {
            var gatewayResponse = await _projectGateway.CanGetProjectAsync(companyId, id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(id);
            if (project == null || project.CompanyId != companyId) return NotFound();

            return Ok(_mapper.Map<ProjectResponse>(project));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project {ProjectId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProjects(string companyId)
    {
        try
        {
            var gatewayResponse = await _projectGateway.CanListProjectsAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var projects = await _projectRepository.GetByCompanyIdAsync(companyId);
            return Ok(_mapper.Map<List<ProjectResponse>>(projects));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing projects for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceProject(string companyId, [FromBody] ProjectCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (string.IsNullOrEmpty(commandModel.Id))
                return BadRequest(new BadRequestResponse { Message = "Id is required" });

            var gatewayResponse = await _projectGateway.CanReplaceProjectAsync(companyId, commandModel.Id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existing = await _projectRepository.GetAsync(commandModel.Id);
            if (existing == null || existing.CompanyId != companyId) return NotFound();

            _mapper.Map(commandModel, existing);
            var updated = await _projectRepository.UpdateAsync(existing);

            _logger.LogInformation("Project replaced: {ProjectId}", updated.Id);
            return Ok(_mapper.Map<ProjectResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replacing project");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PatchProject(string companyId, string id, [FromBody] PatchEntityCommandModel patchModel)
    {
        try
        {
            var gatewayResponse = await _projectGateway.CanUpdateProjectAsync(companyId, id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existing = await _projectRepository.GetAsync(id);
            if (existing == null || existing.CompanyId != companyId) return NotFound();

            existing.ApplyPatch(patchModel);
            var updated = await _projectRepository.UpdateAsync(existing);

            _logger.LogInformation("Project patched: {ProjectId}", updated.Id);
            return Ok(_mapper.Map<ProjectResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error patching project {ProjectId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteProject(string companyId, string id)
    {
        try
        {
            var gatewayResponse = await _projectGateway.CanDeleteProjectAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existing = await _projectRepository.GetAsync(id);
            if (existing == null || existing.CompanyId != companyId) return NotFound();

            await _projectRepository.RemoveAsync(id);

            _logger.LogInformation("Project soft-deleted: {ProjectId}", id);
            await _auditService.LogAsync(companyId, id, "Deleted", "Project", id, _appUser);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}/settings")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProjectSettings(string companyId, string id, [FromBody] UpdateProjectSettingsRequest request)
    {
        try
        {
            var gatewayResponse = await _projectGateway.CanUpdateProjectAsync(companyId, id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(id);
            if (project == null || project.CompanyId != companyId) return NotFound();

            project.Settings ??= new ProjectSettings();

            if (request.AssignmentMode.HasValue)
                project.Settings.AssignmentMode = request.AssignmentMode.Value;
            if (request.DefaultLanguage != null)
                project.Settings.DefaultLanguage = request.DefaultLanguage;
            if (request.AllowUserLanguageOverride.HasValue)
                project.Settings.AllowUserLanguageOverride = request.AllowUserLanguageOverride.Value;

            if (request.Smtp != null)
            {
                var existingSmtpPassword = project.Settings.Smtp?.Password ?? "";
                var smtpPassword = !string.IsNullOrEmpty(request.Smtp.Password)
                    ? EncryptionHelper.Encrypt(request.Smtp.Password)
                    : existingSmtpPassword;
                project.Settings.Smtp = new SmtpSettings
                {
                    Host = request.Smtp.Host,
                    Port = request.Smtp.Port,
                    Username = request.Smtp.Username,
                    Password = smtpPassword,
                    UseSsl = request.Smtp.UseSsl,
                    FromName = request.Smtp.FromName,
                    FromEmail = request.Smtp.FromEmail
                };
            }

            if (request.Imap != null)
            {
                var existingImapPassword = project.Settings.Imap?.Password ?? "";
                var imapPassword = !string.IsNullOrEmpty(request.Imap.Password)
                    ? EncryptionHelper.Encrypt(request.Imap.Password)
                    : existingImapPassword;
                project.Settings.Imap = new ImapSettings
                {
                    Host = request.Imap.Host,
                    Port = request.Imap.Port,
                    Username = request.Imap.Username,
                    Password = imapPassword,
                    UseSsl = request.Imap.UseSsl
                };
            }

            if (request.TicketId != null)
            {
                project.Settings.TicketId ??= new TicketIdSettings();
                if (request.TicketId.Prefix != null)
                    project.Settings.TicketId.Prefix = request.TicketId.Prefix;
                if (request.TicketId.MinLength.HasValue)
                {
                    if (request.TicketId.MinLength.Value < 3)
                        return BadRequest(new BadRequestResponse { Message = "Minimum length cannot be less than 3" });
                    project.Settings.TicketId.MinLength = request.TicketId.MinLength.Value;
                }
                if (request.TicketId.MaxLength.HasValue)
                {
                    if (request.TicketId.MaxLength.Value < project.Settings.TicketId.MinLength)
                        return BadRequest(new BadRequestResponse { Message = "Maximum length cannot be less than minimum length" });
                    project.Settings.TicketId.MaxLength = request.TicketId.MaxLength.Value;
                }
                if (request.TicketId.Format.HasValue)
                    project.Settings.TicketId.Format = request.TicketId.Format.Value;
                if (request.TicketId.SubjectTemplate != null)
                    project.Settings.TicketId.SubjectTemplate = request.TicketId.SubjectTemplate;
                if (request.TicketId.StartingNumber.HasValue)
                {
                    if (request.TicketId.StartingNumber.Value < 0)
                        return BadRequest(new BadRequestResponse { Message = "Starting number cannot be negative" });
                    project.Settings.TicketId.StartingNumber = request.TicketId.StartingNumber.Value;
                    // Ensure counter is at least the starting number
                    var currentValue = await _ticketIdCounterRepository.GetCurrentValueAsync(project.Id);
                    if (currentValue < request.TicketId.StartingNumber.Value)
                        await _ticketIdCounterRepository.SetValueAsync(project.Id, request.TicketId.StartingNumber.Value);
                }
                if (request.TicketId.UseRandomNumbers.HasValue)
                    project.Settings.TicketId.UseRandomNumbers = request.TicketId.UseRandomNumbers.Value;
            }

            if (request.ContactForm != null)
            {
                project.Settings.ContactForm = new ContactFormSettings
                {
                    Enabled = request.ContactForm.Enabled,
                    SystemSenderEmails = request.ContactForm.SystemSenderEmails,
                    EmailLabel = request.ContactForm.EmailLabel,
                    NameLabel = request.ContactForm.NameLabel,
                    MessageLabel = request.ContactForm.MessageLabel,
                    SubjectLabel = request.ContactForm.SubjectLabel
                };
            }

            if (request.EmailSignature != null)
            {
                project.Settings.EmailSignature = new EmailSignatureSettings
                {
                    Enabled = request.EmailSignature.Enabled,
                    SignatureHtml = request.EmailSignature.SignatureHtml,
                    AllowUserOverride = request.EmailSignature.AllowUserOverride
                };
            }

            if (request.Spam != null)
            {
                project.Settings.Spam = new SpamSettings
                {
                    AutoDenyThreshold = Math.Max(1, request.Spam.AutoDenyThreshold),
                    FlagThreshold = Math.Max(1, request.Spam.FlagThreshold),
                    AutoDenyEnabled = request.Spam.AutoDenyEnabled
                };
            }

            if (request.AutoReply != null)
            {
                project.Settings.AutoReply = new AutoReplySettings
                {
                    Enabled = request.AutoReply.Enabled,
                    SubjectTemplate = request.AutoReply.SubjectTemplate,
                    BodyTemplate = request.AutoReply.BodyTemplate
                };
            }

            if (request.StaleTicketDays.HasValue)
                project.Settings.StaleTicketDays = Math.Max(1, request.StaleTicketDays.Value);

            var updated = await _projectRepository.UpdateAsync(project);

            _logger.LogInformation("Project settings updated: {ProjectId}", id);
            await _auditService.LogAsync(companyId, id, "SettingsUpdated", "Project", id, _appUser);
            return Ok(_mapper.Map<ProjectResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project settings {ProjectId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/test-imap")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> TestImap(string companyId, string id, [FromBody] TestConnectionRequest request,
        [FromServices] IConnectionTestService connectionTest)
    {
        var project = await _projectRepository.GetAsync(id);
        if (project == null || project.CompanyId != companyId) return NotFound();

        var password = !string.IsNullOrEmpty(request.Password)
            ? request.Password
            : EncryptionHelper.Decrypt(project.Settings.Imap?.Password ?? "");

        var (success, message) = await connectionTest.TestImapAsync(
            request.Host, request.Port, request.Username, password, request.UseSsl);

        return Ok(new { success, message });
    }

    [HttpPost("{id}/test-smtp")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> TestSmtp(string companyId, string id, [FromBody] TestConnectionRequest request,
        [FromServices] IConnectionTestService connectionTest)
    {
        var project = await _projectRepository.GetAsync(id);
        if (project == null || project.CompanyId != companyId) return NotFound();

        var password = !string.IsNullOrEmpty(request.Password)
            ? request.Password
            : EncryptionHelper.Decrypt(project.Settings.Smtp?.Password ?? "");

        var (success, message) = await connectionTest.TestSmtpAsync(
            request.Host, request.Port, request.Username, password, request.UseSsl);

        return Ok(new { success, message });
    }

    [HttpGet("{id}/members")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<ProjectMemberResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectMembers(string companyId, string id)
    {
        try
        {
            var gatewayResponse = await _projectGateway.CanGetProjectAsync(companyId, id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var companyUsers = await _userRepository.GetByCompanyIdAsync(companyId);
            var members = companyUsers
                .Where(u => u.ProjectMemberships.Any(pm => pm.ProjectId == id))
                .Select(u => new ProjectMemberResponse
                {
                    UserId = u.Id,
                    Email = u.Email,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    Role = u.ProjectMemberships.First(pm => pm.ProjectId == id).Role
                })
                .ToList();

            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project members for {ProjectId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/members")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ProjectMemberResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddProjectMember(string companyId, string id, [FromBody] AddProjectMemberRequest request)
    {
        try
        {
            var gatewayResponse = await _projectGateway.CanUpdateProjectAsync(companyId, id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var user = await _userRepository.GetAsync(request.UserId);
            if (user == null) return NotFound(new BadRequestResponse { Message = "User not found" });

            if (!user.CompanyMemberships.Any(cm => cm.CompanyId == companyId))
                return BadRequest(new BadRequestResponse { Message = "User is not a member of this company" });

            if (user.ProjectMemberships.Any(pm => pm.ProjectId == id))
                return BadRequest(new BadRequestResponse { Message = "User is already a member of this project" });

            user.ProjectMemberships.Add(new ProjectMembership
            {
                CompanyId = companyId,
                ProjectId = id,
                Role = request.Role
            });

            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("User {UserId} added to project {ProjectId} as {Role}", request.UserId, id, request.Role);
            return Ok(new ProjectMemberResponse
            {
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                Role = request.Role
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to project {ProjectId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}/members/{userId}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveProjectMember(string companyId, string id, string userId)
    {
        try
        {
            var gatewayResponse = await _projectGateway.CanUpdateProjectAsync(companyId, id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var user = await _userRepository.GetAsync(userId);
            if (user == null) return NotFound();

            user.ProjectMemberships.RemoveAll(pm => pm.ProjectId == id);
            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("User {UserId} removed from project {ProjectId}", userId, id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from project {ProjectId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}

public class UpdateProjectSettingsRequest
{
    public AssignmentMode? AssignmentMode { get; set; }
    public string? DefaultLanguage { get; set; }
    public bool? AllowUserLanguageOverride { get; set; }
    public UpdateSmtpSettingsRequest? Smtp { get; set; }
    public UpdateImapSettingsRequest? Imap { get; set; }
    public UpdateTicketIdSettingsRequest? TicketId { get; set; }
    public UpdateContactFormSettingsRequest? ContactForm { get; set; }
    public UpdateEmailSignatureRequest? EmailSignature { get; set; }
    public UpdateAutoReplyRequest? AutoReply { get; set; }
    public UpdateSpamSettingsRequest? Spam { get; set; }
    public int? StaleTicketDays { get; set; }
}

public class UpdateSmtpSettingsRequest
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
}

public class UpdateImapSettingsRequest
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
}

public class UpdateTicketIdSettingsRequest
{
    public string? Prefix { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public TicketIdFormat? Format { get; set; }
    public string? SubjectTemplate { get; set; }
    public long? StartingNumber { get; set; }
    public bool? UseRandomNumbers { get; set; }
}

public class UpdateContactFormSettingsRequest
{
    public bool Enabled { get; set; }
    public List<string> SystemSenderEmails { get; set; } = new();
    public string EmailLabel { get; set; } = "Email";
    public string NameLabel { get; set; } = "Name";
    public string MessageLabel { get; set; } = "Message";
    public string? SubjectLabel { get; set; }
}

public class UpdateEmailSignatureRequest
{
    public bool Enabled { get; set; }
    public string SignatureHtml { get; set; } = string.Empty;
    public bool AllowUserOverride { get; set; } = true;
}

public class UpdateSpamSettingsRequest
{
    public int AutoDenyThreshold { get; set; } = 80;
    public int FlagThreshold { get; set; } = 50;
    public bool AutoDenyEnabled { get; set; }
}

public class UpdateAutoReplyRequest
{
    public bool Enabled { get; set; }
    public string SubjectTemplate { get; set; } = "Re: {TicketSubject}";
    public string BodyTemplate { get; set; } = string.Empty;
}

public class TestConnectionRequest
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
}

public class AddProjectMemberRequest
{
    public string UserId { get; set; } = string.Empty;
    public ProjectRole Role { get; set; } = ProjectRole.Agent;
}

public class ProjectMemberResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public ProjectRole Role { get; set; }
}
