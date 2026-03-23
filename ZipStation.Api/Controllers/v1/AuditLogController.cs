using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Repositories;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;
using ZipStation.Models.SearchProfiles;
using MongoDB.Driver;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/audit-log")]
[Authorize]
public class AuditLogController : BaseController
{
    private readonly ILogger<AuditLogController> _logger;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMapper _mapper;
    private readonly IAuditLogGateway _auditLogGateway;

    public AuditLogController(
        ILogger<AuditLogController> logger,
        IAuditLogRepository auditLogRepository,
        IMapper mapper,
        IAuditLogGateway auditLogGateway)
    {
        _logger = logger;
        _auditLogRepository = auditLogRepository;
        _mapper = mapper;
        _auditLogGateway = auditLogGateway;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PaginatedResponse<AuditLogEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAuditLog(
        string companyId,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? query = null,
        [FromQuery] long? fromDate = null,
        [FromQuery] long? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int resultsPerPage = 50)
    {
        try
        {
            var gatewayResponse = await _auditLogGateway.CanViewAuditLogAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var filter = Builders<AuditLogEntry>.Filter.Eq(a => a.CompanyId, companyId);

            if (!string.IsNullOrWhiteSpace(action))
                filter &= Builders<AuditLogEntry>.Filter.Eq(a => a.Action, action);

            if (!string.IsNullOrWhiteSpace(entityType))
                filter &= Builders<AuditLogEntry>.Filter.Eq(a => a.EntityType, entityType);

            if (fromDate.HasValue)
                filter &= Builders<AuditLogEntry>.Filter.Gte(a => a.CreatedOnDateTime, fromDate.Value);

            if (toDate.HasValue)
                filter &= Builders<AuditLogEntry>.Filter.Lte(a => a.CreatedOnDateTime, toDate.Value);

            // Search across action, entityType, userDisplayName, details
            // Each word must match at least one field (AND between words, OR between fields)
            if (!string.IsNullOrWhiteSpace(query))
            {
                var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var term in terms)
                {
                    var regex = new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(term), "i");
                    var termFilters = new List<FilterDefinition<AuditLogEntry>>
                    {
                        Builders<AuditLogEntry>.Filter.Regex(a => a.Action, regex),
                        Builders<AuditLogEntry>.Filter.Regex(a => a.EntityType, regex),
                        Builders<AuditLogEntry>.Filter.Regex(a => a.UserDisplayName, regex),
                        Builders<AuditLogEntry>.Filter.Regex(a => a.Details, regex)
                    };
                    filter &= Builders<AuditLogEntry>.Filter.Or(termFilters);
                }
            }

            var searchProfile = new BaseSearchProfile
            {
                Page = page,
                ResultsPerPage = resultsPerPage,
                OrderByFieldName = "createdOnDateTime",
                OrderByAscending = false
            };

            var result = await _auditLogRepository.GetPaginatedResults(filter, searchProfile);

            return Ok(new PaginatedResponse<AuditLogEntryResponse>
            {
                TotalResultCount = result.TotalResultCount,
                Results = _mapper.Map<List<AuditLogEntryResponse>>(result.Results)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing audit log for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("entity/{entityType}/{entityId}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<AuditLogEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntityAuditLog(string companyId, string entityType, string entityId)
    {
        try
        {
            var gatewayResponse = await _auditLogGateway.CanViewAuditLogAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var entries = await _auditLogRepository.GetByEntityAsync(entityType, entityId);
            return Ok(_mapper.Map<List<AuditLogEntryResponse>>(entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit log for {EntityType}/{EntityId}", entityType, entityId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
