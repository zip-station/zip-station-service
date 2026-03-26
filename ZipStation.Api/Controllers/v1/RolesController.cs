using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Constants;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/roles")]
[Authorize]
public class RolesController : BaseController
{
    private readonly ILogger<RolesController> _logger;
    private readonly IRoleRepository _roleRepository;
    private readonly IMapper _mapper;
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public RolesController(
        ILogger<RolesController> logger,
        IRoleRepository roleRepository,
        IMapper mapper,
        IAppUser appUser,
        IPermissionService permissionService)
    {
        _logger = logger;
        _roleRepository = roleRepository;
        _mapper = mapper;
        _appUser = appUser;
        _permissionService = permissionService;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<RoleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRoles(string companyId)
    {
        try
        {
            if (!await _permissionService.HasPermissionAsync(_appUser.UserId!, companyId, Permissions.RolesView))
                return StatusCode(403, new BadRequestResponse { Message = "You do not have permission to view roles" });

            var roles = await _roleRepository.GetByCompanyIdAsync(companyId);
            return Ok(_mapper.Map<List<RoleResponse>>(roles));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing roles for company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRole(string companyId, string id)
    {
        try
        {
            if (!await _permissionService.HasPermissionAsync(_appUser.UserId!, companyId, Permissions.RolesView))
                return StatusCode(403, new BadRequestResponse { Message = "You do not have permission to view roles" });

            var role = await _roleRepository.GetAsync(id);
            if (role == null || role.CompanyId != companyId) return NotFound();

            return Ok(_mapper.Map<RoleResponse>(role));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role {RoleId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateRole(string companyId, [FromBody] RoleCommandModel commandModel)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!await _permissionService.HasPermissionAsync(_appUser.UserId!, companyId, Permissions.RolesCreate))
                return StatusCode(403, new BadRequestResponse { Message = "You do not have permission to create roles" });

            // Validate permissions are real
            var invalidPerms = commandModel.Permissions.Where(p => !Permissions.All.Contains(p)).ToList();
            if (invalidPerms.Count > 0)
                return BadRequest(new BadRequestResponse { Message = $"Invalid permissions: {string.Join(", ", invalidPerms)}" });

            // Check for duplicate name
            var existing = await _roleRepository.GetByNameAndCompanyAsync(commandModel.Name, companyId);
            if (existing != null)
                return BadRequest(new BadRequestResponse { Message = "A role with this name already exists" });

            var role = _mapper.Map<Role>(commandModel);
            role.CompanyId = companyId;
            role.IsSystem = false;

            var created = await _roleRepository.CreateAsync(role);

            _logger.LogInformation("Role created: {RoleId} in company {CompanyId}", created.Id, companyId);
            return Ok(_mapper.Map<RoleResponse>(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role in company {CompanyId}", companyId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRole(string companyId, string id, [FromBody] RoleCommandModel commandModel)
    {
        try
        {
            if (!await _permissionService.HasPermissionAsync(_appUser.UserId!, companyId, Permissions.RolesEdit))
                return StatusCode(403, new BadRequestResponse { Message = "You do not have permission to edit roles" });

            var role = await _roleRepository.GetAsync(id);
            if (role == null || role.CompanyId != companyId) return NotFound();

            if (role.IsSystem)
                return BadRequest(new BadRequestResponse { Message = "System roles cannot be edited" });

            // Validate permissions are real
            var invalidPerms = commandModel.Permissions.Where(p => !Permissions.All.Contains(p)).ToList();
            if (invalidPerms.Count > 0)
                return BadRequest(new BadRequestResponse { Message = $"Invalid permissions: {string.Join(", ", invalidPerms)}" });

            // Check for duplicate name (exclude self)
            var existing = await _roleRepository.GetByNameAndCompanyAsync(commandModel.Name, companyId);
            if (existing != null && existing.Id != id)
                return BadRequest(new BadRequestResponse { Message = "A role with this name already exists" });

            role.Name = commandModel.Name;
            role.Description = commandModel.Description;
            role.Permissions = commandModel.Permissions;

            var updated = await _roleRepository.UpdateAsync(role);

            _logger.LogInformation("Role updated: {RoleId}", id);
            return Ok(_mapper.Map<RoleResponse>(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteRole(string companyId, string id)
    {
        try
        {
            if (!await _permissionService.HasPermissionAsync(_appUser.UserId!, companyId, Permissions.RolesDelete))
                return StatusCode(403, new BadRequestResponse { Message = "You do not have permission to delete roles" });

            var role = await _roleRepository.GetAsync(id);
            if (role == null || role.CompanyId != companyId) return NotFound();

            if (role.IsSystem)
                return BadRequest(new BadRequestResponse { Message = "System roles cannot be deleted" });

            await _roleRepository.RemoveAsync(id);

            _logger.LogInformation("Role deleted: {RoleId}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", id);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Returns all available permissions grouped by category.
    /// Used by the role management UI to render permission checkboxes.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(Dictionary<string, string[]>), StatusCodes.Status200OK)]
    public IActionResult ListPermissions()
    {
        return Ok(Permissions.Groups);
    }

    /// <summary>
    /// Returns the current user's effective permissions for a company.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/companies/{companyId}/my-permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(MyPermissionsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPermissions(string companyId)
    {
        try
        {
            if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
                return Unauthorized();

            var isOwner = await _permissionService.IsOwnerAsync(_appUser.UserId, companyId);
            var permissions = await _permissionService.GetAllPermissionsAsync(_appUser.UserId, companyId);

            return Ok(new MyPermissionsResponse
            {
                IsOwner = isOwner,
                Permissions = permissions.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for current user");
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
