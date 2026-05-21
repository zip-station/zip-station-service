using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
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
    private readonly IMaxTaskRepository _taskRepository;
    private readonly IMaxQuestionRepository _questionRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly IKanbanCardRepository _kanbanCardRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAnthropicTestService _anthropicTestService;
    private readonly IMaxToneAnalyzerService _toneAnalyzerService;
    private readonly IAuditService _auditService;
    private readonly IAppUser _appUser;
    private readonly IMapper _mapper;

    public MaxController(
        ILogger<MaxController> logger,
        IMaxGateway maxGateway,
        IMaxInstructionRepository instructionRepository,
        IMaxExampleReplyRepository exampleReplyRepository,
        IMaxTaskRepository taskRepository,
        IMaxQuestionRepository questionRepository,
        ITicketRepository ticketRepository,
        IKanbanCardRepository kanbanCardRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IAnthropicTestService anthropicTestService,
        IMaxToneAnalyzerService toneAnalyzerService,
        IAuditService auditService,
        IAppUser appUser,
        IMapper mapper)
    {
        _logger = logger;
        _maxGateway = maxGateway;
        _instructionRepository = instructionRepository;
        _exampleReplyRepository = exampleReplyRepository;
        _taskRepository = taskRepository;
        _questionRepository = questionRepository;
        _ticketRepository = ticketRepository;
        _kanbanCardRepository = kanbanCardRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _anthropicTestService = anthropicTestService;
        _toneAnalyzerService = toneAnalyzerService;
        _auditService = auditService;
        _appUser = appUser;
        _mapper = mapper;
    }

    [HttpGet("tasks")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<MaxTaskWithTicketResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPendingTasks(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanViewAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var pending = await _taskRepository.GetPendingByProjectIdAsync(projectId);
            if (pending.Count == 0)
                return Ok(new List<MaxTaskWithTicketResponse>());

            // Split pending tasks by what they target. A task with StoryId set is a
            // story-side task; everything else is treated as a ticket-side task.
            var storyTasks = pending.Where(t => !string.IsNullOrEmpty(t.StoryId)).ToList();
            var ticketTasks = pending.Where(t => string.IsNullOrEmpty(t.StoryId) && !string.IsNullOrEmpty(t.TicketId)).ToList();

            var result = new List<MaxTaskWithTicketResponse>();

            // ----- Ticket-side rows -----
            if (ticketTasks.Count > 0)
            {
                var ticketIds = ticketTasks.Select(t => t.TicketId).Distinct().ToList();
                var ticketCollection = _ticketRepository.GetCollection();
                var ticketFilter = MongoDB.Driver.Builders<Models.Entities.Ticket>.Filter.In(t => t.Id, ticketIds)
                                 & MongoDB.Driver.Builders<Models.Entities.Ticket>.Filter.Eq(t => t.IsVoid, false);
                var tickets = await ticketCollection.Find(ticketFilter).ToListAsync();
                var ticketsById = tickets.ToDictionary(t => t.Id);

                result.AddRange(ticketTasks
                    .Where(t => ticketsById.ContainsKey(t.TicketId))
                    .Where(t =>
                    {
                        // Closed/Resolved/Merged/Abandoned tickets shouldn't keep showing pending suggestions.
                        var s = ticketsById[t.TicketId].Status;
                        return s == Models.Enums.TicketStatus.Open || s == Models.Enums.TicketStatus.Pending;
                    })
                    .Select(t =>
                    {
                        var ticket = ticketsById[t.TicketId];
                        return new MaxTaskWithTicketResponse
                        {
                            Task = _mapper.Map<MaxTaskResponse>(t),
                            TicketNumber = ticket.TicketNumber,
                            TicketSubject = ticket.Subject,
                            CustomerName = ticket.CustomerName,
                            CustomerEmail = ticket.CustomerEmail,
                        };
                    }));
            }

            // ----- Story-side rows -----
            if (storyTasks.Count > 0)
            {
                var storyIds = storyTasks.Select(t => t.StoryId!).Distinct().ToList();
                var cardCollection = _kanbanCardRepository.GetCollection();
                var cardFilter = MongoDB.Driver.Builders<Models.Entities.KanbanCard>.Filter.In(c => c.Id, storyIds)
                               & MongoDB.Driver.Builders<Models.Entities.KanbanCard>.Filter.Eq(c => c.IsVoid, false);
                var cards = await cardCollection.Find(cardFilter).ToListAsync();
                var cardsById = cards.ToDictionary(c => c.Id);

                result.AddRange(storyTasks
                    .Where(t => cardsById.ContainsKey(t.StoryId!))
                    .Select(t =>
                    {
                        var card = cardsById[t.StoryId!];
                        return new MaxTaskWithTicketResponse
                        {
                            Task = _mapper.Map<MaxTaskResponse>(t),
                            StoryCardNumber = card.CardNumber,
                            StoryTitle = card.Title,
                        };
                    }));
            }

            // Most recent first (already the per-bucket order; merge keeps stable per side
            // but sort the combined list so the maintainer sees newest signals on top).
            result = result.OrderByDescending(r => r.Task.CreatedOnDateTime).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Max tasks for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    // ---------- Questions ----------

    [HttpGet("questions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<MaxQuestionWithSourceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPendingQuestions(string companyId, string projectId, [FromQuery] string status = "pending")
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanViewAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var normalized = status?.ToLowerInvariant() switch
            {
                "answered" => "answered",
                "dismissed" => "dismissed",
                _ => "pending",
            };

            var questions = await _questionRepository.GetByStatusAndProjectIdAsync(projectId, normalized);
            if (questions.Count == 0)
                return Ok(new List<MaxQuestionWithSourceResponse>());

            var ticketQuestions = questions.Where(q => !string.IsNullOrEmpty(q.SourceTicketId)).ToList();
            var storyQuestions = questions.Where(q => !string.IsNullOrEmpty(q.SourceStoryId)).ToList();

            var result = new List<MaxQuestionWithSourceResponse>();

            if (ticketQuestions.Count > 0)
            {
                var ticketIds = ticketQuestions.Select(q => q.SourceTicketId!).Distinct().ToList();
                var ticketCollection = _ticketRepository.GetCollection();
                var ticketFilter = MongoDB.Driver.Builders<Models.Entities.Ticket>.Filter.In(t => t.Id, ticketIds)
                                 & MongoDB.Driver.Builders<Models.Entities.Ticket>.Filter.Eq(t => t.IsVoid, false);
                var tickets = await ticketCollection.Find(ticketFilter).ToListAsync();
                var ticketsById = tickets.ToDictionary(t => t.Id);

                result.AddRange(ticketQuestions
                    .Where(q => ticketsById.ContainsKey(q.SourceTicketId!))
                    .Select(q =>
                    {
                        var ticket = ticketsById[q.SourceTicketId!];
                        return new MaxQuestionWithSourceResponse
                        {
                            Question = _mapper.Map<MaxQuestionResponse>(q),
                            SourceType = "ticket",
                            TicketNumber = ticket.TicketNumber,
                            TicketSubject = ticket.Subject,
                            CustomerName = ticket.CustomerName,
                        };
                    }));
            }

            if (storyQuestions.Count > 0)
            {
                var storyIds = storyQuestions.Select(q => q.SourceStoryId!).Distinct().ToList();
                var cardCollection = _kanbanCardRepository.GetCollection();
                var cardFilter = MongoDB.Driver.Builders<Models.Entities.KanbanCard>.Filter.In(c => c.Id, storyIds)
                               & MongoDB.Driver.Builders<Models.Entities.KanbanCard>.Filter.Eq(c => c.IsVoid, false);
                var cards = await cardCollection.Find(cardFilter).ToListAsync();
                var cardsById = cards.ToDictionary(c => c.Id);

                result.AddRange(storyQuestions
                    .Where(q => cardsById.ContainsKey(q.SourceStoryId!))
                    .Select(q =>
                    {
                        var card = cardsById[q.SourceStoryId!];
                        return new MaxQuestionWithSourceResponse
                        {
                            Question = _mapper.Map<MaxQuestionResponse>(q),
                            SourceType = "story",
                            StoryCardNumber = card.CardNumber,
                            StoryTitle = card.Title,
                        };
                    }));
            }

            result = result.OrderByDescending(r => r.Question.CreatedOnDateTime).ToList();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Max questions for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("questions/{id}/answer")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxQuestionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AnswerQuestion(string companyId, string projectId, string id, [FromBody] MaxQuestionAnswerRequest request)
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            if (string.IsNullOrWhiteSpace(request?.Answer))
                return BadRequest(new BadRequestResponse { Message = "Answer is required" });

            var question = await _questionRepository.GetAsync(id);
            if (question == null || question.CompanyId != companyId || question.ProjectId != projectId)
                return NotFound();

            question.Answer = request.Answer.Trim();
            question.Status = "answered";
            question.AnsweredOnDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            question.PromotedToContext = request.PromoteToContext;
            var updated = await _questionRepository.UpdateAsync(question);

            if (request.PromoteToContext)
            {
                var project = await _projectRepository.GetAsync(projectId);
                if (project != null && project.Settings?.Max != null)
                {
                    var current = project.Settings.Max.ProjectContext ?? string.Empty;
                    var addition = $"Q: {question.Question}\nA: {question.Answer}";
                    project.Settings.Max.ProjectContext = string.IsNullOrWhiteSpace(current)
                        ? addition
                        : current.TrimEnd() + "\n\n---\n" + addition;
                    await _projectRepository.UpdateAsync(project);
                }
            }

            await _auditService.LogAsync(companyId, projectId, "MaxQuestionAnswered", "MaxQuestion", id, _appUser,
                request.PromoteToContext ? "promoted to context" : null);
            return Ok(_mapper.Map<MaxQuestionResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering Max question {QuestionId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("questions/{id}/dismiss")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxQuestionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DismissQuestion(string companyId, string projectId, string id)
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var question = await _questionRepository.GetAsync(id);
            if (question == null || question.CompanyId != companyId || question.ProjectId != projectId)
                return NotFound();

            question.Status = "dismissed";
            var updated = await _questionRepository.UpdateAsync(question);

            await _auditService.LogAsync(companyId, projectId, "MaxQuestionDismissed", "MaxQuestion", id, _appUser);
            return Ok(_mapper.Map<MaxQuestionResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing Max question {QuestionId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
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

    [HttpPost("tone-analyzer")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxToneAnalyzerResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunToneAnalyzer(string companyId, string projectId, [FromBody] MaxToneAnalyzerCommandModel commandModel)
    {
        try
        {
            var gatewayResponse = await _maxGateway.CanAnalyzeToneAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            var (success, error, result) = await _toneAnalyzerService.AnalyzeAsync(projectId, commandModel.ReplyCount ?? 25);
            if (!success || result == null)
                return BadRequest(new BadRequestResponse { Message = error ?? "Tone analysis failed." });

            await _auditService.LogAsync(companyId, projectId, "MaxToneAnalyzerRun", "Project", projectId, _appUser);

            return Ok(new MaxToneAnalyzerResponse
            {
                ToneGuide = result.ToneGuide,
                ToneAvoid = result.ToneAvoid,
                RecommendedExampleIndices = result.RecommendedExampleIndices,
                Replies = result.Replies,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Max tone analyzer for project {ProjectId}", projectId);
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
