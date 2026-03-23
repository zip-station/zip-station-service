using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Enums;
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
    private readonly IUserRepository _userRepository;

    public ProjectGateway(IAppUser appUser, IUserRepository userRepository)
    {
        _appUser = appUser;
        _userRepository = userRepository;
    }

    public async Task<GatewayResponse> CanCreateProjectAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    public async Task<GatewayResponse> CanGetProjectAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var user = await GetCurrentUser();
        if (user == null) return Unauthorized("User not found");

        var hasProjectAccess = user.ProjectMemberships.Any(m => m.ProjectId == projectId && m.CompanyId == companyId);
        var isCompanyAdmin = user.CompanyMemberships.Any(m => m.CompanyId == companyId && (int)m.Role <= (int)CompanyRole.Admin);

        if (!hasProjectAccess && !isCompanyAdmin)
            return Unauthorized("You do not have access to this project");

        return Ok();
    }

    public async Task<GatewayResponse> CanReplaceProjectAsync(string companyId, string projectId)
    {
        return await RequireProjectAdminOrCompanyAdmin(companyId, projectId);
    }

    public async Task<GatewayResponse> CanUpdateProjectAsync(string companyId, string projectId)
    {
        return await RequireProjectAdminOrCompanyAdmin(companyId, projectId);
    }

    public async Task<GatewayResponse> CanDeleteProjectAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    public async Task<GatewayResponse> CanListProjectsAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var user = await GetCurrentUser();
        if (user == null) return Unauthorized("User not found");

        if (!user.CompanyMemberships.Any(m => m.CompanyId == companyId))
            return Unauthorized("You are not a member of this company");

        return Ok();
    }

    private async Task<GatewayResponse> RequireCompanyRole(string companyId, CompanyRole minimumRole)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var user = await GetCurrentUser();
        if (user == null) return Unauthorized("User not found");

        var membership = user.CompanyMemberships.FirstOrDefault(m => m.CompanyId == companyId);
        if (membership == null)
            return Unauthorized("You are not a member of this company");

        if ((int)membership.Role > (int)minimumRole)
            return Unauthorized($"Requires {minimumRole} role or higher");

        return Ok();
    }

    private async Task<GatewayResponse> RequireProjectAdminOrCompanyAdmin(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var user = await GetCurrentUser();
        if (user == null) return Unauthorized("User not found");

        var isCompanyAdmin = user.CompanyMemberships.Any(m => m.CompanyId == companyId && (int)m.Role <= (int)CompanyRole.Admin);
        if (isCompanyAdmin) return Ok();

        var isProjectAdmin = user.ProjectMemberships.Any(m => m.ProjectId == projectId && m.CompanyId == companyId && m.Role == ProjectRole.Admin);
        if (isProjectAdmin) return Ok();

        return Unauthorized("Requires Project Admin or Company Admin role");
    }

    private async Task<Models.Entities.User?> GetCurrentUser()
    {
        return await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
    }

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
