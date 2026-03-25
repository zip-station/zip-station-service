using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Repositories;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/invite")]
public class InviteController : BaseController
{
    private readonly ILogger<InviteController> _logger;
    private readonly IUserRepository _userRepository;

    public InviteController(
        ILogger<InviteController> logger,
        IUserRepository userRepository)
    {
        _logger = logger;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Validate an invite code and return the invited user's email.
    /// No authentication required — this is the first step of the signup flow.
    /// </summary>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(InviteValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateInvite(string code)
    {
        try
        {
            var user = await _userRepository.GetByInviteCodeAsync(code);
            if (user == null)
                return BadRequest(new BadRequestResponse { Message = "Invalid invite code" });

            if (!string.IsNullOrEmpty(user.FirebaseUserId))
                return BadRequest(new BadRequestResponse { Message = "This invite has already been used" });

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (user.InviteCodeExpiresOn > 0 && user.InviteCodeExpiresOn < now)
                return BadRequest(new BadRequestResponse { Message = "This invite has expired" });

            return Ok(new InviteValidationResponse
            {
                Email = user.Email,
                DisplayName = user.DisplayName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating invite code");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Complete signup: link a Firebase user ID to the invited user record.
    /// Called after the SPA creates the Firebase account client-side.
    /// </summary>
    [HttpPost("{code}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteInvite(string code, [FromBody] CompleteInviteCommandModel commandModel)
    {
        try
        {
            var user = await _userRepository.GetByInviteCodeAsync(code);
            if (user == null)
                return BadRequest(new BadRequestResponse { Message = "Invalid invite code" });

            if (!string.IsNullOrEmpty(user.FirebaseUserId))
                return BadRequest(new BadRequestResponse { Message = "This invite has already been used" });

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (user.InviteCodeExpiresOn > 0 && user.InviteCodeExpiresOn < now)
                return BadRequest(new BadRequestResponse { Message = "This invite has expired" });

            // Link the Firebase account to the pre-created user
            user.FirebaseUserId = commandModel.FirebaseUserId;
            if (!string.IsNullOrEmpty(commandModel.DisplayName))
                user.DisplayName = commandModel.DisplayName;
            user.InviteCode = null;
            user.InviteCodeExpiresOn = 0;
            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("Invite completed: user {UserId} linked to Firebase {FirebaseUserId}", user.Id, commandModel.FirebaseUserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing invite");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
