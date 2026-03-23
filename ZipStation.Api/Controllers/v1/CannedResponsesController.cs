using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/projects/{projectId}/canned-responses")]
[Authorize]
public class CannedResponsesController : BaseController
{
    private readonly ILogger<CannedResponsesController> _logger;
    private readonly ICannedResponseRepository _cannedResponseRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IAppUser _appUser;
    private readonly ICannedResponseGateway _cannedResponseGateway;

    public CannedResponsesController(
        ILogger<CannedResponsesController> logger,
        ICannedResponseRepository cannedResponseRepository,
        IUserRepository userRepository,
        IMapper mapper,
        IAppUser appUser,
        ICannedResponseGateway cannedResponseGateway)
    {
        _logger = logger;
        _cannedResponseRepository = cannedResponseRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _appUser = appUser;
        _cannedResponseGateway = cannedResponseGateway;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<CannedResponseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCannedResponses(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _cannedResponseGateway.CanListAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var responses = await _cannedResponseRepository.GetByProjectIdAsync(projectId);
            return Ok(_mapper.Map<List<CannedResponseResponse>>(responses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing canned responses for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CannedResponseResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCannedResponse(string companyId, string projectId, [FromBody] CannedResponseCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _cannedResponseGateway.CanCreateAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            var cannedResponse = _mapper.Map<CannedResponse>(commandModel);
            cannedResponse.CompanyId = companyId;
            cannedResponse.ProjectId = projectId;
            cannedResponse.CreatedByUserId = currentUser?.Id;

            var created = await _cannedResponseRepository.CreateAsync(cannedResponse);

            _logger.LogInformation("Canned response created: {ResponseId} in project {ProjectId}", created.Id, projectId);
            return Ok(_mapper.Map<CannedResponseResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating canned response in project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CannedResponseResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCannedResponse(string companyId, string projectId, string id, [FromBody] CannedResponseCommandModel commandModel)
    {
        try
        {
            var cannedResponse = await _cannedResponseRepository.GetAsync(id);
            if (cannedResponse == null || cannedResponse.CompanyId != companyId || cannedResponse.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _cannedResponseGateway.CanUpdateAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            cannedResponse.Title = commandModel.Title;
            cannedResponse.BodyHtml = commandModel.BodyHtml;
            cannedResponse.Shortcut = commandModel.Shortcut;

            var updated = await _cannedResponseRepository.UpdateAsync(cannedResponse);

            _logger.LogInformation("Canned response updated: {ResponseId}", id);
            return Ok(_mapper.Map<CannedResponseResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating canned response {ResponseId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/use")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CannedResponseResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> IncrementUsage(string companyId, string projectId, string id)
    {
        try
        {
            var cannedResponse = await _cannedResponseRepository.GetAsync(id);
            if (cannedResponse == null || cannedResponse.CompanyId != companyId || cannedResponse.ProjectId != projectId) return NotFound();

            cannedResponse.UsageCount++;
            var updated = await _cannedResponseRepository.UpdateAsync(cannedResponse);

            return Ok(_mapper.Map<CannedResponseResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing usage for canned response {ResponseId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCannedResponse(string companyId, string projectId, string id)
    {
        try
        {
            var cannedResponse = await _cannedResponseRepository.GetAsync(id);
            if (cannedResponse == null || cannedResponse.CompanyId != companyId || cannedResponse.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _cannedResponseGateway.CanDeleteAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            await _cannedResponseRepository.RemoveAsync(id);

            _logger.LogInformation("Canned response deleted: {ResponseId}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting canned response {ResponseId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
