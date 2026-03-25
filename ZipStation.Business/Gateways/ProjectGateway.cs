using ZipStation.Business.Helpers;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface IProjectGateway
{
    Task<GatewayResponse> CanCreateProjectAsync(string companyId);
    Task<GatewayResponse> CanGetProjectAsync(string companyId, string projectId);
    Task<GatewayResponse> CanReplaceProjectAsync(string companyId, string projectId);
    Task<GatewayResponse> CanUpdateProjectAsync(string companyId, string projectId);
    Task<GatewayResponse> CanDeleteProjectAsync(string companyId);
    Task<GatewayResponse> CanListProjectsAsync(string companyId);
}

public class ProjectGateway : IProjectGateway
{
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public ProjectGateway(IAppUser appUser, IPermissionService permissionService)
    {
        _appUser = appUser;
        _permissionService = permissionService;
    }

    public async Task<GatewayResponse> CanCreateProjectAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.ProjectsCreate))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanGetProjectAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.ProjectsView, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanReplaceProjectAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.ProjectsSettings, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanUpdateProjectAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.ProjectsSettings, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanDeleteProjectAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.ProjectsDelete))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanListProjectsAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        // Any authenticated company member can list their accessible projects
        return Ok();
    }

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
