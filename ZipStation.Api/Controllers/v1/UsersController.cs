using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;

using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize]
public class UsersController : BaseController
{
    private readonly ILogger<UsersController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IMapper _mapper;
    private readonly IAppUser _appUser;
    private readonly IUserGateway _userGateway;

    public UsersController(
        ILogger<UsersController> logger,
        IUserRepository userRepository,
        ICompanyRepository companyRepository,
        IMapper mapper,
        IAppUser appUser,
        IUserGateway userGateway)
    {
        _logger = logger;
        _userRepository = userRepository;
        _companyRepository = companyRepository;
        _mapper = mapper;
        _appUser = appUser;
        _userGateway = userGateway;
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] UserCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _userGateway.CanCreateUserAsync();
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existing = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            if (existing != null)
                return Ok(_mapper.Map<UserResponse>(existing));

            // Check if a pre-created user record exists from an invite
            var email = (_appUser.Email ?? commandModel.Email)?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(email))
            {
                var invited = await _userRepository.GetByEmailAsync(email);
                if (invited != null && string.IsNullOrEmpty(invited.FirebaseUserId))
                {
                    // Link the Firebase UID to the invited user record
                    invited.FirebaseUserId = _appUser.UserId!;
                    if (!string.IsNullOrEmpty(commandModel.DisplayName))
                        invited.DisplayName = commandModel.DisplayName;

                    var updated = await _userRepository.UpdateAsync(invited);
                    _logger.LogInformation("Invited user linked: {UserId} ({Email}) with Firebase UID", updated.Id, updated.Email);
                    return Ok(_mapper.Map<UserResponse>(updated));
                }
            }

            var user = _mapper.Map<User>(commandModel);
            user.FirebaseUserId = _appUser.UserId!;
            user.Email = email ?? commandModel.Email;

            var created = await _userRepository.CreateAsync(user);

            _logger.LogInformation("User created: {UserId} ({Email})", created.Id, created.Email);
            return Ok(_mapper.Map<UserResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(string id)
    {
        try
        {
            var gatewayResponse = await _userGateway.CanGetUserAsync(id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var user = await _userRepository.GetAsync(id);
            if (user == null) return NotFound();

            return Ok(_mapper.Map<UserResponse>(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("me")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
                return Unauthorized();

            var user = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId);
            if (user == null) return NotFound();

            var response = _mapper.Map<UserResponse>(user);

            // Check if user is owner of any of their companies
            var companyIds = user.RoleAssignments.Select(ra => ra.CompanyId).Distinct().ToList();
            foreach (var cid in companyIds)
            {
                var company = await _companyRepository.GetAsync(cid);
                if (company != null && company.OwnerUserId == user.Id)
                {
                    response.IsOwner = true;
                    break;
                }
            }
            // Also check if they're an owner even without role assignments (fresh setup)
            if (!response.IsOwner)
            {
                var ownedCompanies = await _companyRepository.GetByOwnerUserIdAsync(user.Id);
                if (ownedCompanies.Any()) response.IsOwner = true;
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceUser([FromBody] UserCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (string.IsNullOrEmpty(commandModel.Id))
                return BadRequest(new BadRequestResponse { Message = "Id is required" });

            var gatewayResponse = await _userGateway.CanReplaceUserAsync(commandModel.Id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existing = await _userRepository.GetAsync(commandModel.Id);
            if (existing == null) return NotFound();

            _mapper.Map(commandModel, existing);
            var updated = await _userRepository.UpdateAsync(existing);

            _logger.LogInformation("User replaced: {UserId}", updated.Id);
            return Ok(_mapper.Map<UserResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replacing user");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PatchUser(string id, [FromBody] PatchEntityCommandModel patchModel)
    {
        try
        {
            var gatewayResponse = await _userGateway.CanUpdateUserAsync(id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existing = await _userRepository.GetAsync(id);
            if (existing == null) return NotFound();

            existing.ApplyPatch(patchModel);
            var updated = await _userRepository.UpdateAsync(existing);

            _logger.LogInformation("User patched: {UserId}", updated.Id);
            return Ok(_mapper.Map<UserResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error patching user {UserId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("me/preferences")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateUserPreferencesRequest request)
    {
        try
        {
            if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
                return Unauthorized();

            var user = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId);
            if (user == null) return NotFound();

            user.Preferences ??= new UserPreferences();

            if (request.PreferredLanguage != null)
                user.Preferences.PreferredLanguage = request.PreferredLanguage;
            if (request.Timezone != null)
                user.Preferences.Timezone = request.Timezone;

            var updated = await _userRepository.UpdateAsync(user);

            _logger.LogInformation("User preferences updated: {UserId}", updated.Id);
            return Ok(_mapper.Map<UserResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user preferences");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("company/{companyId}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompanyMembers(string companyId)
    {
        try
        {
            var gatewayResponse = await _userGateway.CanSearchUsersAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var users = await _userRepository.GetByCompanyIdAsync(companyId);

            // Also include the owner if they don't have role assignments for this company
            var company = await _companyRepository.GetAsync(companyId);
            if (company != null && !string.IsNullOrEmpty(company.OwnerUserId))
            {
                var ownerAlreadyIncluded = users.Any(u => u.Id == company.OwnerUserId);
                if (!ownerAlreadyIncluded)
                {
                    var owner = await _userRepository.GetAsync(company.OwnerUserId);
                    if (owner != null) users.Insert(0, owner);
                }
            }

            var responses = _mapper.Map<List<UserResponse>>(users);

            // Set IsOwner flag
            if (company != null)
            {
                foreach (var r in responses)
                {
                    var user = users.First(u => u.Id == r.Id);
                    r.IsOwner = user.Id == company.OwnerUserId;
                }
            }

            return Ok(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company members for {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("invite")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InviteUser(string companyId, [FromBody] InviteUserRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new BadRequestResponse { Message = "Email is required" });

            var gatewayResponse = await _userGateway.CanInviteUserAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Check if a user with that email already exists
            var existing = await _userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
            if (existing != null)
            {
                // Check if already a member of this company
                if (existing.RoleAssignments.Any(ra => ra.CompanyId == companyId))
                    return BadRequest(new BadRequestResponse { Message = "User is already a member of this company" });

                // Add a company-level role assignment as a placeholder
                existing.RoleAssignments.Add(new RoleAssignment
                {
                    CompanyId = companyId,
                    RoleId = string.Empty,
                    ProjectId = null
                });

                // Add project-level role assignments if specified
                if (request.ProjectIds != null)
                {
                    foreach (var projectId in request.ProjectIds)
                    {
                        if (!existing.RoleAssignments.Any(ra => ra.ProjectId == projectId && ra.CompanyId == companyId))
                        {
                            existing.RoleAssignments.Add(new RoleAssignment
                            {
                                CompanyId = companyId,
                                RoleId = string.Empty,
                                ProjectId = projectId
                            });
                        }
                    }
                }

                var updated = await _userRepository.UpdateAsync(existing);
                _logger.LogInformation("Existing user {Email} added to company {CompanyId}", request.Email, companyId);
                return Ok(_mapper.Map<UserResponse>(updated));
            }

            // Create a new user record (no FirebaseUserId yet — they'll link on sign-up)
            var inviteCode = Guid.NewGuid().ToString("N"); // 32-char hex string
            var user = new User
            {
                Email = request.Email.Trim().ToLowerInvariant(),
                DisplayName = request.DisplayName ?? string.Empty,
                InviteCode = inviteCode,
                InviteCodeExpiresOn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds(),
                RoleAssignments = new List<RoleAssignment>
                {
                    new RoleAssignment
                    {
                        CompanyId = companyId,
                        RoleId = string.Empty,
                        ProjectId = null
                    }
                }
            };

            // Add project-level role assignments if specified
            if (request.ProjectIds != null)
            {
                foreach (var projectId in request.ProjectIds)
                {
                    user.RoleAssignments.Add(new RoleAssignment
                    {
                        CompanyId = companyId,
                        RoleId = string.Empty,
                        ProjectId = projectId
                    });
                }
            }

            var created = await _userRepository.CreateAsync(user);

            _logger.LogInformation("User invited: {Email} to company {CompanyId}", request.Email, companyId);

            // Send invite email in background if company SMTP is configured
            _ = Task.Run(async () =>
            {
                try { await SendInviteEmailAsync(companyId, request.Email, request.DisplayName, inviteCode); }
                catch (Exception ex) { _logger.LogError(ex, "Failed to send invite email to {Email}", request.Email); }
            });

            return Ok(_mapper.Map<UserResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting user to company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/resend-invite")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendInvite(string companyId, string id)
    {
        try
        {
            var user = await _userRepository.GetAsync(id);
            if (user == null) return NotFound();
            if (!string.IsNullOrEmpty(user.FirebaseUserId))
                return BadRequest(new BadRequestResponse { Message = "User has already signed up" });

            // Regenerate invite code
            user.InviteCode = Guid.NewGuid().ToString("N");
            user.InviteCodeExpiresOn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds();
            await _userRepository.UpdateAsync(user);

            await SendInviteEmailAsync(companyId, user.Email, user.DisplayName, user.InviteCode);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending invite to user {UserId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    private async Task SendInviteEmailAsync(string companyId, string toEmail, string? toName, string? inviteCode = null)
    {
        var company = await _companyRepository.GetAsync(companyId);
        var smtp = company?.Settings?.Smtp;
        if (smtp == null || string.IsNullOrEmpty(smtp.Host)) return;

        var fromEmail = smtp.FromEmail ?? smtp.Username;
        var fromName = smtp.FromName ?? company!.Name;

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(fromName, fromEmail));
        mimeMessage.To.Add(new MailboxAddress(toName ?? toEmail, toEmail));
        mimeMessage.Subject = $"You've been invited to {company.Name} on Zip Station";

        var baseUrl = company.Settings?.BaseUrl;
        var signupLink = !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(inviteCode)
            ? $"{baseUrl}/setup?code={inviteCode}" : null;
        var linkHtml = signupLink != null
            ? $"<p><a href=\"{signupLink}\" style=\"display:inline-block;padding:10px 24px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600\">Get Started</a></p>"
            : "<p>Sign up or log in to get started.</p>";
        var linkText = signupLink != null ? $"Get started: {signupLink}" : "Sign up or log in to get started.";

        var displayName = toName ?? toEmail.Split('@')[0];
        var body = $"<p>Hi {displayName},</p>" +
                   $"<p>You've been invited to join <strong>{company.Name}</strong> on Zip Station.</p>" +
                   linkHtml +
                   $"<p>— {company.Name} Team</p>";

        var bodyBuilder = new BodyBuilder { HtmlBody = body, TextBody = $"Hi {displayName},\n\nYou've been invited to join {company.Name} on Zip Station.\n\n{linkText}\n\n— {company.Name} Team" };
        mimeMessage.Body = bodyBuilder.ToMessageBody();

        var password = EncryptionHelper.Decrypt(smtp.Password);
        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (s, c, h, e) => true;
        var secureSocketOptions = smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(smtp.Host, smtp.Port, secureSocketOptions);
        await client.AuthenticateAsync(smtp.Username, password);
        await client.SendAsync(mimeMessage);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Invite email sent to {Email} for company {CompanyId}", toEmail, companyId);
    }
    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteUser(string id, [FromQuery] string companyId)
    {
        try
        {
            if (string.IsNullOrEmpty(companyId))
                return BadRequest(new BadRequestResponse { Message = "companyId is required" });

            // Get the requesting user
            var requestingUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            if (requestingUser == null) return Unauthorized();

            var hasCompanyAccess = requestingUser.RoleAssignments.Any(ra => ra.CompanyId == companyId);
            if (!hasCompanyAccess)
                return StatusCode(403, new BadRequestResponse { Message = "You are not a member of this company" });

            // Cannot delete yourself
            if (requestingUser.Id == id)
                return BadRequest(new BadRequestResponse { Message = "You cannot delete yourself" });

            // Get the target user
            var targetUser = await _userRepository.GetAsync(id);
            if (targetUser == null) return NotFound();

            var targetHasCompanyAccess = targetUser.RoleAssignments.Any(ra => ra.CompanyId == companyId);
            if (!targetHasCompanyAccess)
                return BadRequest(new BadRequestResponse { Message = "Target user is not a member of this company" });

            // Permission check: only company owner can delete members
            var company = await _companyRepository.GetAsync(companyId);
            if (company == null) return NotFound();
            if (company.OwnerUserId != requestingUser.Id)
                return StatusCode(403, new BadRequestResponse { Message = "You do not have permission to remove members" });

            // Remove all role assignments for this company
            targetUser.RoleAssignments.RemoveAll(ra => ra.CompanyId == companyId);

            if (targetUser.RoleAssignments.Count == 0)
            {
                // Soft-delete the user if no remaining company memberships
                targetUser.IsVoid = true;
                await _userRepository.UpdateAsync(targetUser);

                // Delete the Firebase account so the email can be reused
                if (!string.IsNullOrEmpty(targetUser.FirebaseUserId))
                {
                    try
                    {
                        await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance
                            .DeleteUserAsync(targetUser.FirebaseUserId);
                        _logger.LogInformation("Firebase account deleted for user {UserId}", id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete Firebase account for user {UserId}", id);
                    }
                }

                _logger.LogInformation("User {UserId} soft-deleted (no remaining memberships)", id);
            }
            else
            {
                await _userRepository.UpdateAsync(targetUser);
                _logger.LogInformation("User {UserId} removed from company {CompanyId}", id, companyId);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId} from company {CompanyId}", id, companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}/toggle-disable")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ToggleDisableUser(string id, [FromQuery] string companyId)
    {
        try
        {
            if (string.IsNullOrEmpty(companyId))
                return BadRequest(new BadRequestResponse { Message = "companyId is required" });

            // Get the requesting user
            var requestingUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            if (requestingUser == null) return Unauthorized();

            var hasCompanyAccess = requestingUser.RoleAssignments.Any(ra => ra.CompanyId == companyId);
            if (!hasCompanyAccess)
                return StatusCode(403, new BadRequestResponse { Message = "You are not a member of this company" });

            // Cannot disable yourself
            if (requestingUser.Id == id)
                return BadRequest(new BadRequestResponse { Message = "You cannot disable yourself" });

            // Get the target user
            var targetUser = await _userRepository.GetAsync(id);
            if (targetUser == null) return NotFound();

            var targetHasCompanyAccess = targetUser.RoleAssignments.Any(ra => ra.CompanyId == companyId);
            if (!targetHasCompanyAccess)
                return BadRequest(new BadRequestResponse { Message = "Target user is not a member of this company" });

            // Permission check: only company owner can disable members
            var company = await _companyRepository.GetAsync(companyId);
            if (company == null) return NotFound();
            if (company.OwnerUserId != requestingUser.Id)
                return StatusCode(403, new BadRequestResponse { Message = "You do not have permission to disable members" });

            // Toggle the disabled state
            targetUser.IsDisabled = !targetUser.IsDisabled;
            var updated = await _userRepository.UpdateAsync(targetUser);

            _logger.LogInformation("User {UserId} {State} by {RequestingUserId}",
                id, updated.IsDisabled ? "disabled" : "enabled", requestingUser.Id);

            return Ok(_mapper.Map<UserResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling disable for user {UserId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
    [HttpPost("{id}/transfer-ownership")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TransferOwnership(string id, [FromQuery] string companyId)
    {
        try
        {
            if (string.IsNullOrEmpty(companyId))
                return BadRequest(new BadRequestResponse { Message = "companyId is required" });

            var requestingUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            if (requestingUser == null) return Unauthorized();

            var company = await _companyRepository.GetAsync(companyId);
            if (company == null) return NotFound();

            // Only the current owner can transfer ownership
            if (company.OwnerUserId != requestingUser.Id)
                return StatusCode(403, new BadRequestResponse { Message = "Only the current owner can transfer ownership" });

            // Cannot transfer to yourself
            if (requestingUser.Id == id)
                return BadRequest(new BadRequestResponse { Message = "You are already the owner" });

            // Target must exist and be in the company
            var targetUser = await _userRepository.GetAsync(id);
            if (targetUser == null) return NotFound();

            if (!targetUser.RoleAssignments.Any(ra => ra.CompanyId == companyId))
                return BadRequest(new BadRequestResponse { Message = "Target user is not a member of this company" });

            // Transfer ownership
            company.OwnerUserId = targetUser.Id;
            await _companyRepository.UpdateAsync(company);

            // Ensure the new owner has a company-wide role assignment
            if (!targetUser.RoleAssignments.Any(ra => ra.CompanyId == companyId && ra.ProjectId == null))
            {
                targetUser.RoleAssignments.Add(new RoleAssignment
                {
                    CompanyId = companyId,
                    RoleId = string.Empty,
                    ProjectId = null
                });
                await _userRepository.UpdateAsync(targetUser);
            }

            _logger.LogInformation("Ownership of company {CompanyId} transferred from {OldOwner} to {NewOwner}",
                companyId, requestingUser.Id, targetUser.Id);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring ownership of company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}

public class UpdateUserPreferencesRequest
{
    public string? PreferredLanguage { get; set; }
    public string? Timezone { get; set; }
}

public class InviteUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public List<string>? ProjectIds { get; set; }
}
