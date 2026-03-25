using ZipStation.Business.Helpers;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface IAlertGateway
{
    Task<GatewayResponse> CanListAlertsAsync(string companyId);
    Task<GatewayResponse> CanCreateAlertAsync(string companyId);
    Task<GatewayResponse> CanUpdateAlertAsync(string companyId);
    Task<GatewayResponse> CanDeleteAlertAsync(string companyId);
}

public class AlertGateway : IAlertGateway
{
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public AlertGateway(IAppUser appUser, IPermissionService permissionService)
    {
        _appUser = appUser;
        _permissionService = permissionService;
    }

    public async Task<GatewayResponse> CanListAlertsAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.AlertsView))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanCreateAlertAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.AlertsCreate))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanUpdateAlertAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.AlertsEdit))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanDeleteAlertAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.AlertsDelete))
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
