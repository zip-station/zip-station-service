using ZipStation.Business.Helpers;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface IAuditLogGateway
{
    Task<GatewayResponse> CanViewAuditLogAsync(string companyId);
}

public class AuditLogGateway : IAuditLogGateway
{
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public AuditLogGateway(IAppUser appUser, IPermissionService permissionService)
    {
        _appUser = appUser;
        _permissionService = permissionService;
    }

    public async Task<GatewayResponse> CanViewAuditLogAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.AuditLogView))
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
