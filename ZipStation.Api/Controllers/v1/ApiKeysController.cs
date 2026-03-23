using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/projects/{projectId}/api-keys")]
[Authorize]
public class ApiKeysController : BaseController
{
    private readonly ILogger<ApiKeysController> _logger;
    private readonly IProjectApiKeyRepository _apiKeyRepository;
    private readonly IProjectGateway _projectGateway;
    private readonly IAppUser _appUser;
    private readonly IUserRepository _userRepository;
    private readonly IAuditService _auditService;

    public ApiKeysController(
        ILogger<ApiKeysController> logger,
        IProjectApiKeyRepository apiKeyRepository,
        IProjectGateway projectGateway,
        IAppUser appUser,
        IUserRepository userRepository,
        IAuditService auditService)
    {
        _logger = logger;
        _apiKeyRepository = apiKeyRepository;
        _projectGateway = projectGateway;
        _appUser = appUser;
        _userRepository = userRepository;
        _auditService = auditService;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<ProjectApiKeyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListKeys(string companyId, string projectId)
    {
        var gatewayResponse = await _projectGateway.CanGetProjectAsync(companyId, projectId);
        if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
            return ProcessGatewayResponse(gatewayResponse);

        var keys = await _apiKeyRepository.GetByProjectIdAsync(projectId);
        var responses = keys.Select(k => new ProjectApiKeyResponse
        {
            Id = k.Id,
            CompanyId = k.CompanyId,
            ProjectId = k.ProjectId,
            Name = k.Name,
            KeyPrefix = k.KeyPrefix,
            IsRevoked = k.IsRevoked,
            CreatedByUserId = k.CreatedByUserId,
            CreatedOnDateTime = k.CreatedOnDateTime
        }).ToList();

        return Ok(responses);
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ProjectApiKeyCreatedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateKey(string companyId, string projectId, [FromBody] CreateApiKeyRequest request)
    {
        var gatewayResponse = await _projectGateway.CanUpdateProjectAsync(companyId, projectId);
        if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
            return ProcessGatewayResponse(gatewayResponse);

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new BadRequestResponse { Message = "Name is required" });

        // Generate a random API key
        var rawKey = $"zs_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "").Replace("/", "").Replace("=", "")}";
        var keyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
        var keyPrefix = rawKey[..12];

        var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

        var apiKey = new ProjectApiKey
        {
            CompanyId = companyId,
            ProjectId = projectId,
            Name = request.Name,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            CreatedByUserId = currentUser?.Id
        };

        var created = await _apiKeyRepository.CreateAsync(apiKey);

        await _auditService.LogAsync(companyId, projectId, "Created", "ApiKey", created.Id, _appUser, $"Name: {request.Name}");
        _logger.LogInformation("API key created for project {ProjectId}: {KeyPrefix}...", projectId, keyPrefix);

        return Ok(new ProjectApiKeyCreatedResponse
        {
            Id = created.Id,
            CompanyId = created.CompanyId,
            ProjectId = created.ProjectId,
            Name = created.Name,
            KeyPrefix = created.KeyPrefix,
            IsRevoked = false,
            CreatedByUserId = created.CreatedByUserId,
            CreatedOnDateTime = created.CreatedOnDateTime,
            FullKey = rawKey // Only returned once on creation
        });
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeKey(string companyId, string projectId, string id)
    {
        var gatewayResponse = await _projectGateway.CanUpdateProjectAsync(companyId, projectId);
        if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
            return ProcessGatewayResponse(gatewayResponse);

        var key = await _apiKeyRepository.GetAsync(id);
        if (key == null || key.ProjectId != projectId) return NotFound();

        key.IsRevoked = true;
        await _apiKeyRepository.UpdateAsync(key);

        await _auditService.LogAsync(companyId, projectId, "Revoked", "ApiKey", id, _appUser, $"Name: {key.Name}");
        _logger.LogInformation("API key revoked: {KeyId}", id);

        return Ok();
    }
}

public class CreateApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
}
