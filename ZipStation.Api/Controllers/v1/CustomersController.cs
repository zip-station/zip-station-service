using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Gateways;
using ZipStation.Business.Repositories;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;
using ZipStation.Models.SearchProfiles;
using MongoDB.Driver;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/customers")]
[Authorize]
public class CustomersController : BaseController
{
    private readonly ILogger<CustomersController> _logger;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMapper _mapper;
    private readonly ICustomerGateway _customerGateway;

    public CustomersController(
        ILogger<CustomersController> logger,
        ICustomerRepository customerRepository,
        IMapper mapper,
        ICustomerGateway customerGateway)
    {
        _logger = logger;
        _customerRepository = customerRepository;
        _mapper = mapper;
        _customerGateway = customerGateway;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PaginatedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCustomers(
        string companyId,
        [FromQuery] string? projectId = null,
        [FromQuery] string? email = null,
        [FromQuery] string? query = null,
        [FromQuery] int page = 1,
        [FromQuery] int resultsPerPage = 25)
    {
        try
        {
            var gatewayResponse = await _customerGateway.CanListCustomersAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var filter = Builders<Customer>.Filter.Eq(c => c.CompanyId, companyId);

            if (!string.IsNullOrEmpty(projectId))
                filter &= Builders<Customer>.Filter.Eq(c => c.ProjectId, projectId);

            if (!string.IsNullOrEmpty(email))
                filter &= Builders<Customer>.Filter.Eq(c => c.Email, email);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var regex = new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query), "i");
                filter &= Builders<Customer>.Filter.Or(
                    Builders<Customer>.Filter.Regex(c => c.Name, regex),
                    Builders<Customer>.Filter.Regex(c => c.Email, regex)
                );
            }

            var searchProfile = new BaseSearchProfile
            {
                Page = page,
                ResultsPerPage = resultsPerPage,
                OrderByFieldName = "createdOnDateTime",
                OrderByAscending = false
            };

            var result = await _customerRepository.GetPaginatedResults(filter, searchProfile);

            return Ok(new PaginatedResponse<CustomerResponse>
            {
                TotalResultCount = result.TotalResultCount,
                Results = _mapper.Map<List<CustomerResponse>>(result.Results)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing customers for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomer(string companyId, string id)
    {
        try
        {
            var customer = await _customerRepository.GetAsync(id);
            if (customer == null || customer.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _customerGateway.CanGetCustomerAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            return Ok(_mapper.Map<CustomerResponse>(customer));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer {CustomerId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCustomer(string companyId, [FromBody] CustomerCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var gatewayResponse = await _customerGateway.CanCreateCustomerAsync(companyId, commandModel.ProjectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            // Check for existing customer with same email in project
            var existing = await _customerRepository.GetByEmailAndProjectAsync(commandModel.Email, commandModel.ProjectId);
            if (existing != null)
                return BadRequest(new BadRequestResponse { Message = "A customer with this email already exists in this project" });

            var customer = _mapper.Map<Customer>(commandModel);
            customer.CompanyId = companyId;

            var created = await _customerRepository.CreateAsync(customer);

            _logger.LogInformation("Customer created: {CustomerId} in project {ProjectId}", created.Id, created.ProjectId);
            return Ok(_mapper.Map<CustomerResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer in company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCustomer(string companyId, string id, [FromBody] CustomerCommandModel commandModel)
    {
        try
        {
            var customer = await _customerRepository.GetAsync(id);
            if (customer == null || customer.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _customerGateway.CanUpdateCustomerAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            customer.Name = commandModel.Name;
            customer.Tags = commandModel.Tags;
            customer.Notes = commandModel.Notes;
            customer.IsBanned = commandModel.IsBanned;
            customer.Properties = commandModel.Properties;

            var updated = await _customerRepository.UpdateAsync(customer);

            _logger.LogInformation("Customer updated: {CustomerId}", id);
            return Ok(_mapper.Map<CustomerResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer {CustomerId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCustomer(string companyId, string id)
    {
        try
        {
            var customer = await _customerRepository.GetAsync(id);
            if (customer == null || customer.CompanyId != companyId) return NotFound();

            var gatewayResponse = await _customerGateway.CanDeleteCustomerAsync(companyId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            await _customerRepository.RemoveAsync(id);

            _logger.LogInformation("Customer deleted: {CustomerId}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer {CustomerId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
