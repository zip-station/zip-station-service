using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies")]
[Authorize]
public class CompaniesController : BaseController
{
    private readonly ILogger<CompaniesController> _logger;
    private readonly ICompanyRepository _companyRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IAppUser _appUser;
    private readonly ICompanyGateway _companyGateway;

    public CompaniesController(
        ILogger<CompaniesController> logger,
        ICompanyRepository companyRepository,
        IUserRepository userRepository,
        IMapper mapper,
        IAppUser appUser,
        ICompanyGateway companyGateway)
    {
        _logger = logger;
        _companyRepository = companyRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _appUser = appUser;
        _companyGateway = companyGateway;
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCompany([FromBody] CompanyCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _companyGateway.CanCreateCompanyAsync();
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existingSlug = await _companyRepository.GetBySlugAsync(commandModel.Slug);
            if (existingSlug != null)
                return BadRequest(new BadRequestResponse { Message = "A company with this slug already exists" });

            var company = _mapper.Map<Company>(commandModel);
            company.OwnerUserId = _appUser.UserId!;

            var created = await _companyRepository.CreateAsync(company);

            // Add the creator as Owner of this company
            var user = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            if (user != null)
            {
                user.CompanyMemberships.Add(new CompanyMembership
                {
                    CompanyId = created.Id,
                    Role = CompanyRole.Owner
                });
                await _userRepository.UpdateAsync(user);
            }

            _logger.LogInformation("Company created: {CompanyId} by user {UserId}", created.Id, _appUser.UserId);
            return Ok(_mapper.Map<CompanyResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCompany(string id)
    {
        try
        {
            var gatewayResponse = await _companyGateway.CanGetCompanyAsync(id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var company = await _companyRepository.GetAsync(id);
            if (company == null) return NotFound();

            return Ok(_mapper.Map<CompanyResponse>(company));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company {CompanyId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<CompanyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCompanies()
    {
        try
        {
            var gatewayResponse = await _companyGateway.CanListCompaniesAsync();
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var user = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
            if (user == null) return Ok(new List<CompanyResponse>());

            var companyIds = user.CompanyMemberships.Select(m => m.CompanyId).ToList();
            var companies = await _companyRepository.GetByIdsAsync(companyIds);

            return Ok(_mapper.Map<List<CompanyResponse>>(companies));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing companies");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceCompany([FromBody] CompanyCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (string.IsNullOrEmpty(commandModel.Id)) return BadRequest(new BadRequestResponse { Message = "Id is required" });

            var gatewayResponse = await _companyGateway.CanReplaceCompanyAsync(commandModel.Id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existing = await _companyRepository.GetAsync(commandModel.Id);
            if (existing == null) return NotFound();

            _mapper.Map(commandModel, existing);
            var updated = await _companyRepository.UpdateAsync(existing);

            _logger.LogInformation("Company replaced: {CompanyId}", updated.Id);
            return Ok(_mapper.Map<CompanyResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replacing company");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PatchCompany(string id, [FromBody] PatchEntityCommandModel patchModel)
    {
        try
        {
            var gatewayResponse = await _companyGateway.CanUpdateCompanyAsync(id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existing = await _companyRepository.GetAsync(id);
            if (existing == null) return NotFound();

            existing.ApplyPatch(patchModel);
            var updated = await _companyRepository.UpdateAsync(existing);

            _logger.LogInformation("Company patched: {CompanyId}", updated.Id);
            return Ok(_mapper.Map<CompanyResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error patching company {CompanyId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPatch("{id}/settings")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCompanySettings(string id, [FromBody] UpdateCompanySettingsCommandModel request)
    {
        try
        {
            var gatewayResponse = await _companyGateway.CanUpdateCompanyAsync(id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var company = await _companyRepository.GetAsync(id);
            if (company == null) return NotFound();

            company.Settings ??= new CompanySettings();

            if (request.DefaultTimezone != null)
                company.Settings.DefaultTimezone = request.DefaultTimezone;
            if (request.DefaultLanguage != null)
                company.Settings.DefaultLanguage = request.DefaultLanguage;

            if (request.Smtp != null)
            {
                company.Settings.Smtp ??= new SmtpSettings();
                company.Settings.Smtp.Host = request.Smtp.Host;
                company.Settings.Smtp.Port = request.Smtp.Port;
                company.Settings.Smtp.Username = request.Smtp.Username;
                company.Settings.Smtp.UseSsl = request.Smtp.UseSsl;
                company.Settings.Smtp.FromName = request.Smtp.FromName;
                company.Settings.Smtp.FromEmail = request.Smtp.FromEmail;
                if (!string.IsNullOrEmpty(request.Smtp.Password))
                    company.Settings.Smtp.Password = EncryptionHelper.Encrypt(request.Smtp.Password);
            }

            var updated = await _companyRepository.UpdateAsync(company);
            _logger.LogInformation("Company settings updated: {CompanyId}", id);
            return Ok(_mapper.Map<CompanyResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company settings {CompanyId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("{id}/test-smtp")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> TestSmtp(string id, [FromBody] TestConnectionRequest request,
        [FromServices] Business.Services.IConnectionTestService connectionTest)
    {
        var company = await _companyRepository.GetAsync(id);
        if (company == null) return NotFound();

        var password = !string.IsNullOrEmpty(request.Password)
            ? request.Password
            : EncryptionHelper.Decrypt(company.Settings?.Smtp?.Password ?? "");

        var (success, message) = await connectionTest.TestSmtpAsync(
            request.Host, request.Port, request.Username, password, request.UseSsl);

        return Ok(new { success, message });
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCompany(string id)
    {
        try
        {
            var gatewayResponse = await _companyGateway.CanDeleteCompanyAsync(id);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var existing = await _companyRepository.GetAsync(id);
            if (existing == null) return NotFound();

            await _companyRepository.RemoveAsync(id);

            _logger.LogInformation("Company soft-deleted: {CompanyId}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company {CompanyId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
