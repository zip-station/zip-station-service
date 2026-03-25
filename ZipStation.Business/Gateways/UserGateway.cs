using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
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
    private readonly IPermissionService _permissionService;

    public UserGateway(IAppUser appUser, IUserRepository userRepository, IPermissionService permissionService)
    {
        _appUser = appUser;
        _userRepository = userRepository;
        _permissionService = permissionService;
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

        var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId);
        if (currentUser == null) return Unauthorized("User not found");

        // Users can always view themselves
        if (currentUser.Id == userId) return Ok();

        // Check if the target user exists
        var targetUser = await _userRepository.GetAsync(userId);
        if (targetUser == null) return NotFound("User not found");

        // Check if they share a company via role assignments
        var sharedCompany = currentUser.RoleAssignments
            .Any(cr => targetUser.RoleAssignments.Any(tr => tr.CompanyId == cr.CompanyId));
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
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.MembersView))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanInviteUserAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.MembersInvite))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    private async Task<GatewayResponse> CanEditUser(string userId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var currentUser = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId);
        if (currentUser == null) return Unauthorized("User not found");

        // Users can always edit themselves
        if (currentUser.Id == userId) return Ok();

        var targetUser = await _userRepository.GetAsync(userId);
        if (targetUser == null) return NotFound("User not found");

        // Check if the current user has MembersEdit permission in any shared company
        foreach (var ra in currentUser.RoleAssignments)
        {
            if (targetUser.RoleAssignments.Any(tr => tr.CompanyId == ra.CompanyId))
            {
                if (await _permissionService.HasPermissionAsync(_appUser.UserId, ra.CompanyId, Permissions.MembersEdit))
                    return Ok();
            }
        }

        return Unauthorized("Insufficient permissions");
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
