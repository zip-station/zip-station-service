using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Enums;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface IUserGateway
{
    Task<GatewayResponse> CanCreateUserAsync();
    Task<GatewayResponse> CanGetUserAsync(string userId);
    Task<GatewayResponse> CanReplaceUserAsync(string userId);
    Task<GatewayResponse> CanUpdateUserAsync(string userId);
    Task<GatewayResponse> CanSearchUsersAsync(string companyId);
    Task<GatewayResponse> CanInviteUserAsync(string companyId);
}

public class UserGateway : IUserGateway
{
    private readonly IAppUser _appUser;
    private readonly IUserRepository _userRepository;

    public UserGateway(IAppUser appUser, IUserRepository userRepository)
    {
        _appUser = appUser;
        _userRepository = userRepository;
    }

    public async Task<GatewayResponse> CanCreateUserAsync()
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        return Ok();
    }

    public async Task<GatewayResponse> CanGetUserAsync(string userId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var currentUser = await GetCurrentUser();
        if (currentUser == null) return Unauthorized("User not found");

        if (currentUser.Id == userId) return Ok();

        var targetUser = await _userRepository.GetAsync(userId);
        if (targetUser == null) return NotFound("User not found");

        var sharedCompany = currentUser.CompanyMemberships
            .Any(cm => targetUser.CompanyMemberships.Any(tm => tm.CompanyId == cm.CompanyId));
        if (!sharedCompany) return Unauthorized("No shared company membership");

        return Ok();
    }

    public async Task<GatewayResponse> CanReplaceUserAsync(string userId)
    {
        return await CanEditUser(userId);
    }

    public async Task<GatewayResponse> CanUpdateUserAsync(string userId)
    {
        return await CanEditUser(userId);
    }

    public async Task<GatewayResponse> CanSearchUsersAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    public async Task<GatewayResponse> CanInviteUserAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    private async Task<GatewayResponse> CanEditUser(string userId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var currentUser = await GetCurrentUser();
        if (currentUser == null) return Unauthorized("User not found");

        if (currentUser.Id == userId) return Ok();

        var targetUser = await _userRepository.GetAsync(userId);
        if (targetUser == null) return NotFound("User not found");

        var isAdminOfSharedCompany = currentUser.CompanyMemberships
            .Where(cm => (int)cm.Role <= (int)CompanyRole.Admin)
            .Any(cm => targetUser.CompanyMemberships.Any(tm => tm.CompanyId == cm.CompanyId));

        if (!isAdminOfSharedCompany) return Unauthorized("Requires Company Admin role");

        return Ok();
    }

    private async Task<GatewayResponse> RequireCompanyRole(string companyId, CompanyRole minimumRole)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var user = await GetCurrentUser();
        if (user == null) return Unauthorized("User not found");

        var membership = user.CompanyMemberships.FirstOrDefault(m => m.CompanyId == companyId);
        if (membership == null) return Unauthorized("You are not a member of this company");

        if ((int)membership.Role > (int)minimumRole)
            return Unauthorized($"Requires {minimumRole} role or higher");

        return Ok();
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
    private static GatewayResponse NotFound(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.NotFound,
        ResponseMessage = msg ?? "Not found"
    };
}
