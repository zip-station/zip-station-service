using ZipStation.Business.Helpers;
using ZipStation.Business.Services;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface IPersonalAccessTokenGateway
{
    Task<GatewayResponse> CanManageAsync(string companyId);
}

public class PersonalAccessTokenGateway : IPersonalAccessTokenGateway
{
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public PersonalAccessTokenGateway(IAppUser appUser, IPermissionService permissionService)
    {
        _appUser = appUser;
        _permissionService = permissionService;
    }

    public async Task<GatewayResponse> CanManageAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        // PATs are personal — any member of the company can manage their own.
        // Owners and members both have at least one role assignment / owner status,
        // which yields a non-empty permission set.
        var permissions = await _permissionService.GetAllPermissionsAsync(_appUser.UserId, companyId);
        if (permissions.Count == 0)
            return Unauthorized("Not a member of this company");

        return Ok();
    }

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
