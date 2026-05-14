using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/projects/{projectId}/max")]
[Authorize]
public class MaxController : BaseController
{
    private readonly ILogger<MaxController> _logger;
    private readonly IMaxGateway _maxGateway;
    private readonly IMaxInstructionRepository _instructionRepository;
    private readonly IMaxExampleReplyRepository _exampleReplyRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAnthropicTestService _anthropicTestService;
    private readonly IAuditService _auditService;
    private readonly IAppUser _appUser;
    private readonly IMapper _mapper;

    public MaxController(
        ILogger<MaxController> logger,
        IMaxGateway maxGateway,
        IMaxInstructionRepository instructionRepository,
        IMaxExampleReplyRepository exampleReplyRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IAnthropicTestService anthropicTestService,
        IAuditService auditService,
        IAppUser appUser,
        IMapper mapper)
    {
        _logger = logger;
        _maxGateway = maxGateway;
        _instructionRepository = instructionRepository;
        _exampleReplyRepository = exampleReplyRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _anthropicTestService = anthropicTestService;
        _auditService = auditService;
        _appUser = appUser;
        _mapper = mapper;
    }

    // ---------- API key ----------

    [HttpPut("api-key")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetApiKey(string companyId, string projectId, [FromBody] MaxApiKeyCommandModel commandModel)
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            project.Settings ??= new ProjectSettings();
            project.Settings.Max ??= new MaxSettings();

            if (string.IsNullOrEmpty(commandModel.ApiKey))
            {
                project.Settings.Max.ApiKeyEncrypted = string.Empty;
                // Disabling once the key is gone keeps state coherent for the worker (Phase 2+).
                project.Settings.Max.Enabled = false;
            }
            else
            {
                project.Settings.Max.ApiKeyEncrypted = EncryptionHelper.Encrypt(commandModel.ApiKey);
            }

            await _projectRepository.UpdateAsync(project);

            _logger.LogInformation("Max API key updated for project {ProjectId}", projectId);
            await _auditService.LogAsync(companyId, projectId, "MaxApiKeyUpdated", "Project", projectId, _appUser);

            return Ok(_mapper.Map<MaxSettingsResponse>(project.Settings.Max));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Max API key for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("reset")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reset(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            if (project.Settings?.Max != null)
            {
                project.Settings.Max.ProjectContext = null;
                project.Settings.Max.ToneGuide = null;
                project.Settings.Max.ToneAvoid = null;
                await _projectRepository.UpdateAsync(project);
            }

            var instructions = await _instructionRepository.GetByProjectIdAsync(projectId);
            foreach (var inst in instructions)
                await _instructionRepository.RemoveAsync(inst.Id);

            var examples = await _exampleReplyRepository.GetByProjectIdAsync(projectId);
            foreach (var ex in examples)
                await _exampleReplyRepository.RemoveAsync(ex.Id);

            _logger.LogInformation(
                "Max reset for project {ProjectId}: cleared context/tone, soft-deleted {InstructionCount} instructions and {ExampleCount} example replies",
                projectId, instructions.Count, examples.Count);

            await _auditService.LogAsync(
                companyId, projectId, "MaxReset", "Project", projectId, _appUser,
                $"Cleared project context, tone guide, tone avoid, {instructions.Count} instructions, {examples.Count} example replies");

            return Ok(new { instructionsCleared = instructions.Count, exampleRepliesCleared = examples.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting Max for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("test-connection")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxTestConnectionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestConnection(string companyId, string projectId, [FromBody] MaxTestConnectionCommandModel commandModel)
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanTestConnectionAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            var apiKey = !string.IsNullOrEmpty(commandModel.ApiKey)
                ? commandModel.ApiKey
                : EncryptionHelper.Decrypt(project.Settings?.Max?.ApiKeyEncrypted ?? "");

            if (string.IsNullOrEmpty(apiKey))
            {
                return Ok(new MaxTestConnectionResponse { Success = false, Message = "No API key configured." });
            }

            var model = !string.IsNullOrWhiteSpace(commandModel.Model)
                ? commandModel.Model
                : (project.Settings?.Max?.Model ?? "claude-sonnet-4-6");

            var (success, message) = await _anthropicTestService.TestConnectionAsync(apiKey, model);
            return Ok(new MaxTestConnectionResponse { Success = success, Message = message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Max connection for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ---------- Instructions ----------

    [HttpGet("instructions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<MaxInstructionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInstructions(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanViewAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var instructions = await _instructionRepository.GetByProjectIdAsync(projectId);
            return Ok(_mapper.Map<List<MaxInstructionResponse>>(instructions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Max instructions for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("instructions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxInstructionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateInstruction(string companyId, string projectId, [FromBody] MaxInstructionCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            var instruction = _mapper.Map<MaxInstruction>(commandModel);
            instruction.CompanyId = companyId;
            instruction.ProjectId = projectId;
            instruction.Source = "manual";
            instruction.CreatedByUserId = currentUser?.Id;

            var created = await _instructionRepository.CreateAsync(instruction);

            _logger.LogInformation("Max instruction created: {InstructionId} in project {ProjectId}", created.Id, projectId);
            return Ok(_mapper.Map<MaxInstructionResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Max instruction in project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("instructions/{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxInstructionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateInstruction(string companyId, string projectId, string id, [FromBody] MaxInstructionCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var instruction = await _instructionRepository.GetAsync(id);
            if (instruction == null || instruction.CompanyId != companyId || instruction.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            instruction.Instruction = commandModel.Instruction;
            instruction.Contexts = commandModel.Contexts;

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            instruction.UpdatedByUserId = currentUser?.Id;

            var updated = await _instructionRepository.UpdateAsync(instruction);
            return Ok(_mapper.Map<MaxInstructionResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Max instruction {InstructionId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("instructions/{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteInstruction(string companyId, string projectId, string id)
    {
        try
        {
            var instruction = await _instructionRepository.GetAsync(id);
            if (instruction == null || instruction.CompanyId != companyId || instruction.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            await _instructionRepository.RemoveAsync(id);
            _logger.LogInformation("Max instruction deleted: {InstructionId}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Max instruction {InstructionId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ---------- Example replies ----------

    [HttpGet("example-replies")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<MaxExampleReplyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListExampleReplies(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanViewAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var replies = await _exampleReplyRepository.GetByProjectIdAsync(projectId);
            return Ok(_mapper.Map<List<MaxExampleReplyResponse>>(replies));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Max example replies for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("example-replies")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxExampleReplyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateExampleReply(string companyId, string projectId, [FromBody] MaxExampleReplyCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            var reply = _mapper.Map<MaxExampleReply>(commandModel);
            reply.CompanyId = companyId;
            reply.ProjectId = projectId;
            reply.CreatedByUserId = currentUser?.Id;

            var created = await _exampleReplyRepository.CreateAsync(reply);

            _logger.LogInformation("Max example reply created: {ReplyId} in project {ProjectId}", created.Id, projectId);
            return Ok(_mapper.Map<MaxExampleReplyResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Max example reply in project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("example-replies/{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxExampleReplyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateExampleReply(string companyId, string projectId, string id, [FromBody] MaxExampleReplyCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var reply = await _exampleReplyRepository.GetAsync(id);
            if (reply == null || reply.CompanyId != companyId || reply.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            reply.ReplyText = commandModel.ReplyText;
            reply.SourceTicketId = commandModel.SourceTicketId;
            reply.Notes = commandModel.Notes;

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            reply.UpdatedByUserId = currentUser?.Id;

            var updated = await _exampleReplyRepository.UpdateAsync(reply);
            return Ok(_mapper.Map<MaxExampleReplyResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Max example reply {ReplyId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("example-replies/{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteExampleReply(string companyId, string projectId, string id)
    {
        try
        {
            var reply = await _exampleReplyRepository.GetAsync(id);
            if (reply == null || reply.CompanyId != companyId || reply.ProjectId != projectId) return NotFound();

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            await _exampleReplyRepository.RemoveAsync(id);
            _logger.LogInformation("Max example reply deleted: {ReplyId}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Max example reply {ReplyId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
