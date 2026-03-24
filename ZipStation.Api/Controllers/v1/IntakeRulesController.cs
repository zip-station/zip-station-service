using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Repositories;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/projects/{projectId}/intake-rules")]
[Authorize]
public class IntakeRulesController : BaseController
{
    private readonly ILogger<IntakeRulesController> _logger;
    private readonly IIntakeRuleRepository _intakeRuleRepository;
    private readonly IIntakeEmailRepository _intakeEmailRepository;
    private readonly IMapper _mapper;
    private readonly IIntakeRuleGateway _intakeRuleGateway;

    public IntakeRulesController(
        ILogger<IntakeRulesController> logger,
        IIntakeRuleRepository intakeRuleRepository,
        IIntakeEmailRepository intakeEmailRepository,
        IMapper mapper,
        IIntakeRuleGateway intakeRuleGateway)
    {
        _logger = logger;
        _intakeRuleRepository = intakeRuleRepository;
        _intakeEmailRepository = intakeEmailRepository;
        _mapper = mapper;
        _intakeRuleGateway = intakeRuleGateway;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<IntakeRuleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRules(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _intakeRuleGateway.CanListRulesAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var rules = await _intakeRuleRepository.GetByProjectIdAsync(projectId);
            return Ok(_mapper.Map<List<IntakeRuleResponse>>(rules));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing intake rules for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IntakeRuleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateRule(string companyId, string projectId, [FromBody] IntakeRuleCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _intakeRuleGateway.CanCreateRuleAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Check for duplicate name
            var existingRules = await _intakeRuleRepository.GetByProjectIdAsync(projectId);
            if (existingRules.Any(r => r.Name.Equals(commandModel.Name, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new BadRequestResponse { Message = "A rule with this name already exists" });

            var rule = _mapper.Map<IntakeRule>(commandModel);
            rule.CompanyId = companyId;
            rule.ProjectId = projectId;

            var created = await _intakeRuleRepository.CreateAsync(rule);

            _logger.LogInformation("Intake rule created: {RuleId} in project {ProjectId}", created.Id, projectId);
            return Ok(_mapper.Map<IntakeRuleResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating intake rule in project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IntakeRuleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRule(string companyId, string projectId, string id, [FromBody] IntakeRuleCommandModel commandModel)
    {
        try
        {
            var rule = await _intakeRuleRepository.GetAsync(id);
            if (rule == null || rule.CompanyId != companyId || rule.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _intakeRuleGateway.CanUpdateRuleAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Check for duplicate name (exclude current rule)
            var existingRules = await _intakeRuleRepository.GetByProjectIdAsync(projectId);
            if (existingRules.Any(r => r.Id != id && r.Name.Equals(commandModel.Name, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new BadRequestResponse { Message = "A rule with this name already exists" });

            rule.Name = commandModel.Name;
            rule.Conditions = commandModel.Conditions
                .Select(c => new IntakeRuleCondition { Type = c.Type, Value = c.Value })
                .ToList();
            rule.Action = commandModel.Action;
            rule.Priority = commandModel.Priority;
            rule.IsEnabled = commandModel.IsEnabled;

            var updated = await _intakeRuleRepository.UpdateAsync(rule);

            _logger.LogInformation("Intake rule updated: {RuleId}", id);
            return Ok(_mapper.Map<IntakeRuleResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating intake rule {RuleId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteRule(string companyId, string projectId, string id)
    {
        try
        {
            var rule = await _intakeRuleRepository.GetAsync(id);
            if (rule == null || rule.CompanyId != companyId || rule.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _intakeRuleGateway.CanDeleteRuleAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            await _intakeRuleRepository.RemoveAsync(id);

            _logger.LogInformation("Intake rule deleted: {RuleId}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting intake rule {RuleId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/run")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(RunRuleResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunRule(string companyId, string projectId, string id)
    {
        try
        {
            var rule = await _intakeRuleRepository.GetAsync(id);
            if (rule == null || rule.CompanyId != companyId || rule.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _intakeRuleGateway.CanUpdateRuleAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            if (rule.Conditions.Count == 0)
                return BadRequest(new BadRequestResponse { Message = "Rule has no conditions" });

            var pendingIntakes = await _intakeEmailRepository.GetPendingByProjectIdAsync(projectId);
            var matched = 0;

            foreach (var intake in pendingIntakes)
            {
                var allMatch = rule.Conditions.All(condition => condition.Type switch
                {
                    IntakeConditionType.FromEmail => intake.FromEmail.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
                    IntakeConditionType.FromDomain => intake.FromEmail.EndsWith($"@{condition.Value.TrimStart('@')}", StringComparison.OrdinalIgnoreCase),
                    IntakeConditionType.SubjectContains => intake.Subject.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
                    IntakeConditionType.BodyContains => intake.BodyText.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
                    _ => false
                });

                if (!allMatch) continue;

                if (rule.Action is IntakeActionType.AutoDeny or IntakeActionType.AutoDenyPermanent)
                {
                    intake.Status = IntakeStatus.Denied;
                    intake.DeniedPermanently = rule.Action == IntakeActionType.AutoDenyPermanent;
                    intake.ProcessedOn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    await _intakeEmailRepository.UpdateAsync(intake);
                }
                // Note: AutoApprove would need ticket creation logic — skip for now
                matched++;
            }

            _logger.LogInformation("Rule {RuleId} run against {Total} pending intakes, {Matched} matched", id, pendingIntakes.Count, matched);
            return Ok(new RunRuleResult { Matched = matched, Total = pendingIntakes.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running intake rule {RuleId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    private static bool MatchesRule(IntakeEmail intake, IntakeRule rule)
    {
        return rule.Conditions.All(condition => condition.Type switch
        {
            IntakeConditionType.FromEmail => intake.FromEmail.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
            IntakeConditionType.FromDomain => intake.FromEmail.EndsWith($"@{condition.Value.TrimStart('@')}", StringComparison.OrdinalIgnoreCase),
            IntakeConditionType.SubjectContains => intake.Subject.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
            IntakeConditionType.BodyContains => intake.BodyText.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        });
    }
}
