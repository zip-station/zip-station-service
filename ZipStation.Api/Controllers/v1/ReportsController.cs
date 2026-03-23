using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/reports")]
[Authorize]
public class ReportsController : BaseController
{
    private readonly ILogger<ReportsController> _logger;
    private readonly IReportRepository _reportRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IAppUser _appUser;

    public ReportsController(
        ILogger<ReportsController> logger,
        IReportRepository reportRepository,
        IUserRepository userRepository,
        IMapper mapper,
        IAppUser appUser)
    {
        _logger = logger;
        _reportRepository = reportRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _appUser = appUser;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<ReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListReports(string companyId)
    {
        try
        {
            var reports = await _reportRepository.GetByCompanyIdAsync(companyId);
            return Ok(_mapper.Map<List<ReportResponse>>(reports));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing reports for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateReport(string companyId, [FromBody] ReportCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);

            var report = _mapper.Map<Report>(commandModel);
            report.CompanyId = companyId;
            report.CreatedByUserId = currentUser?.Id;

            var created = await _reportRepository.CreateAsync(report);

            _logger.LogInformation("Report created: {ReportId} in company {CompanyId}", created.Id, companyId);
            return Ok(_mapper.Map<ReportResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating report in company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateReport(string companyId, string id, [FromBody] ReportCommandModel commandModel)
    {
        try
        {
            var report = await _reportRepository.GetAsync(id);
            if (report == null || report.CompanyId != companyId) return NotFound();

            report.Name = commandModel.Name;
            report.ProjectId = commandModel.ProjectId;
            report.Frequency = commandModel.Frequency;
            report.RecipientEmails = commandModel.RecipientEmails;
            report.IncludeTicketSummary = commandModel.IncludeTicketSummary;
            report.IncludeResponseTimes = commandModel.IncludeResponseTimes;
            report.IncludeAgentPerformance = commandModel.IncludeAgentPerformance;
            report.IncludeCustomerActivity = commandModel.IncludeCustomerActivity;
            report.IsEnabled = commandModel.IsEnabled;

            var updated = await _reportRepository.UpdateAsync(report);

            _logger.LogInformation("Report updated: {ReportId}", id);
            return Ok(_mapper.Map<ReportResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating report {ReportId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteReport(string companyId, string id)
    {
        try
        {
            var report = await _reportRepository.GetAsync(id);
            if (report == null || report.CompanyId != companyId) return NotFound();

            await _reportRepository.RemoveAsync(id);

            _logger.LogInformation("Report deleted: {ReportId}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting report {ReportId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
