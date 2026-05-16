using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/personal-access-tokens")]
[Authorize]
public class PersonalAccessTokensController : BaseController
{
    public const string TokenPrefix = "zs_pat_";

    private readonly ILogger<PersonalAccessTokensController> _logger;
    private readonly IPersonalAccessTokenRepository _patRepository;
    private readonly IPersonalAccessTokenGateway _patGateway;
    private readonly IUserRepository _userRepository;
    private readonly IAppUser _appUser;

    public PersonalAccessTokensController(
        ILogger<PersonalAccessTokensController> logger,
        IPersonalAccessTokenRepository patRepository,
        IPersonalAccessTokenGateway patGateway,
        IUserRepository userRepository,
        IAppUser appUser)
    {
        _logger = logger;
        _patRepository = patRepository;
        _patGateway = patGateway;
        _userRepository = userRepository;
        _appUser = appUser;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<PersonalAccessTokenResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTokens(string companyId)
    {
        try
        {
            var gatewayResponse = await _patGateway.CanManageAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            if (currentUser == null) return Unauthorized();

            var tokens = await _patRepository.GetByUserIdAsync(currentUser.Id, companyId);
            var responses = tokens.Select(ToResponse).ToList();
            return Ok(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing PATs for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PersonalAccessTokenCreatedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateToken(string companyId, [FromBody] CreatePersonalAccessTokenRequest request)
    {
        try
        {
            var gatewayResponse = await _patGateway.CanManageAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new BadRequestResponse { Message = "Name is required" });

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            if (currentUser == null) return Unauthorized();

            var rawToken = TokenPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "").Replace("/", "").Replace("=", "");
            var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
            var tokenPrefix = rawToken[..Math.Min(rawToken.Length, 16)];

            var pat = new PersonalAccessToken
            {
                UserId = currentUser.Id,
                CompanyId = companyId,
                Name = request.Name.Trim(),
                TokenHash = tokenHash,
                TokenPrefix = tokenPrefix,
                ExpiresOnDateTime = request.ExpiresOnDateTime,
                CreatedByUserId = currentUser.Id
            };

            var created = await _patRepository.CreateAsync(pat);

            _logger.LogInformation("PAT created for user {UserId} in company {CompanyId}: {Prefix}...",
                currentUser.Id, companyId, tokenPrefix);

            var response = ToResponse(created);
            return Ok(new PersonalAccessTokenCreatedResponse
            {
                Id = response.Id,
                UserId = response.UserId,
                CompanyId = response.CompanyId,
                Name = response.Name,
                TokenPrefix = response.TokenPrefix,
                IsRevoked = response.IsRevoked,
                CreatedOnDateTime = response.CreatedOnDateTime,
                LastUsedOnDateTime = response.LastUsedOnDateTime,
                ExpiresOnDateTime = response.ExpiresOnDateTime,
                FullToken = rawToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PAT in company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeToken(string companyId, string id)
    {
        try
        {
            var gatewayResponse = await _patGateway.CanManageAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            if (currentUser == null) return Unauthorized();

            var pat = await _patRepository.GetAsync(id);
            if (pat == null || pat.CompanyId != companyId || pat.UserId != currentUser.Id)
                return NotFound();

            pat.IsRevoked = true;
            await _patRepository.UpdateAsync(pat);

            _logger.LogInformation("PAT revoked: {TokenId} by user {UserId}", id, currentUser.Id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking PAT {TokenId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    private static PersonalAccessTokenResponse ToResponse(PersonalAccessToken pat) => new()
    {
        Id = pat.Id,
        UserId = pat.UserId,
        CompanyId = pat.CompanyId,
        Name = pat.Name,
        TokenPrefix = pat.TokenPrefix,
        IsRevoked = pat.IsRevoked,
        CreatedOnDateTime = pat.CreatedOnDateTime,
        LastUsedOnDateTime = pat.LastUsedOnDateTime,
        ExpiresOnDateTime = pat.ExpiresOnDateTime
    };
}

public class CreatePersonalAccessTokenRequest
{
    public string Name { get; set; } = string.Empty;
    public long? ExpiresOnDateTime { get; set; }
}
