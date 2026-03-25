using ZipStation.Business.Helpers;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface IIntakeRuleGateway
{
    Task<GatewayResponse> CanListRulesAsync(string companyId);
    Task<GatewayResponse> CanCreateRuleAsync(string companyId);
    Task<GatewayResponse> CanUpdateRuleAsync(string companyId);
    Task<GatewayResponse> CanDeleteRuleAsync(string companyId);
}

public class IntakeRuleGateway : IIntakeRuleGateway
{
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public IntakeRuleGateway(IAppUser appUser, IPermissionService permissionService)
    {
        _appUser = appUser;
        _permissionService = permissionService;
    }

    public async Task<GatewayResponse> CanListRulesAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.IntakeRulesView))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanCreateRuleAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.IntakeRulesCreate))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanUpdateRuleAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.IntakeRulesEdit))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanDeleteRuleAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.IntakeRulesDelete))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
