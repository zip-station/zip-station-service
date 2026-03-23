using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Repositories;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/projects/{projectId}/alerts")]
[Authorize]
public class AlertsController : BaseController
{
    private readonly ILogger<AlertsController> _logger;
    private readonly IAlertRepository _alertRepository;
    private readonly IMapper _mapper;
    private readonly IAlertGateway _alertGateway;

    public AlertsController(
        ILogger<AlertsController> logger,
        IAlertRepository alertRepository,
        IMapper mapper,
        IAlertGateway alertGateway)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _mapper = mapper;
        _alertGateway = alertGateway;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<AlertResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAlerts(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _alertGateway.CanListAlertsAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var alerts = await _alertRepository.GetByProjectIdAsync(projectId);
            return Ok(_mapper.Map<List<AlertResponse>>(alerts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing alerts for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAlert(string companyId, string projectId, [FromBody] AlertCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _alertGateway.CanCreateAlertAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Check for duplicate name
            var existingAlerts = await _alertRepository.GetByProjectIdAsync(projectId);
            if (existingAlerts.Any(a => a.Name.Equals(commandModel.Name, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new BadRequestResponse { Message = "An alert with this name already exists" });

            var alert = _mapper.Map<Alert>(commandModel);
            alert.CompanyId = companyId;
            alert.ProjectId = projectId;

            var created = await _alertRepository.CreateAsync(alert);

            _logger.LogInformation("Alert created: {AlertId} in project {ProjectId}", created.Id, projectId);
            return Ok(_mapper.Map<AlertResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert in project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAlert(string companyId, string projectId, string id, [FromBody] AlertCommandModel commandModel)
    {
        try
        {
            var alert = await _alertRepository.GetAsync(id);
            if (alert == null || alert.CompanyId != companyId || alert.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _alertGateway.CanUpdateAlertAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Check for duplicate name (exclude current alert)
            var existingAlerts = await _alertRepository.GetByProjectIdAsync(projectId);
            if (existingAlerts.Any(a => a.Id != id && a.Name.Equals(commandModel.Name, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new BadRequestResponse { Message = "An alert with this name already exists" });

            alert.Name = commandModel.Name;
            alert.TriggerType = commandModel.TriggerType;
            alert.TriggerValue = commandModel.TriggerValue;
            alert.ChannelType = commandModel.ChannelType;
            alert.WebhookUrl = commandModel.WebhookUrl;
            alert.CustomPayloadTemplate = commandModel.CustomPayloadTemplate;
            alert.IsEnabled = commandModel.IsEnabled;

            var updated = await _alertRepository.UpdateAsync(alert);

            _logger.LogInformation("Alert updated: {AlertId}", id);
            return Ok(_mapper.Map<AlertResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alert {AlertId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAlert(string companyId, string projectId, string id)
    {
        try
        {
            var alert = await _alertRepository.GetAsync(id);
            if (alert == null || alert.CompanyId != companyId || alert.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _alertGateway.CanDeleteAlertAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            await _alertRepository.RemoveAsync(id);

            _logger.LogInformation("Alert deleted: {AlertId}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alert {AlertId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
