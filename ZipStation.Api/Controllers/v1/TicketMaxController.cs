using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/tickets/{ticketId}/max")]
[Authorize]
public class TicketMaxController : BaseController
{
    private readonly ILogger<TicketMaxController> _logger;
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketGateway _ticketGateway;
    private readonly IMaxGateway _maxGateway;
    private readonly IMaxTicketEnrichmentRepository _enrichmentRepository;
    private readonly IMaxTaskRepository _taskRepository;
    private readonly IMaxQuestionRepository _questionRepository;
    private readonly IMaxEnrichmentService _enrichmentService;
    private readonly IMapper _mapper;

    public TicketMaxController(
        ILogger<TicketMaxController> logger,
        ITicketRepository ticketRepository,
        ITicketGateway ticketGateway,
        IMaxGateway maxGateway,
        IMaxTicketEnrichmentRepository enrichmentRepository,
        IMaxTaskRepository taskRepository,
        IMaxQuestionRepository questionRepository,
        IMaxEnrichmentService enrichmentService,
        IMapper mapper)
    {
        _logger = logger;
        _ticketRepository = ticketRepository;
        _ticketGateway = ticketGateway;
        _maxGateway = maxGateway;
        _enrichmentRepository = enrichmentRepository;
        _taskRepository = taskRepository;
        _questionRepository = questionRepository;
        _enrichmentService = enrichmentService;
        _mapper = mapper;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TicketMaxResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTicketMax(string companyId, string ticketId)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(ticketId);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _ticketGateway.CanGetTicketAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var enrichment = await _enrichmentRepository.GetByTicketIdAsync(ticketId);
            var tasks = await _taskRepository.GetByTicketIdAsync(ticketId);

            // Questions linked to this ticket
            var allQuestions = await _questionRepository.GetByTicketIdAsync(ticketId);

            return Ok(new TicketMaxResponse
            {
                Enrichment = enrichment != null ? _mapper.Map<MaxTicketEnrichmentResponse>(enrichment) : null,
                Tasks = _mapper.Map<List<MaxTaskResponse>>(tasks),
                Questions = _mapper.Map<List<MaxQuestionResponse>>(allQuestions),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Max data for ticket {TicketId}", ticketId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("enrich")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MaxTicketEnrichmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReEnrich(string companyId, string ticketId)
    {
        try
        {
            var ticket = await _ticketRepository.GetAsync(ticketId);
            if (ticket == null || ticket.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _maxGateway.CanEditAsync(companyId, ticket.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var enrichment = await _enrichmentService.EnrichTicketAsync(ticketId);
            if (enrichment == null)
                return BadRequest(new BadRequestResponse { Message = "Enrichment did not produce a result. See server logs for details." });

            return Ok(_mapper.Map<MaxTicketEnrichmentResponse>(enrichment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-enriching ticket {TicketId}", ticketId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
