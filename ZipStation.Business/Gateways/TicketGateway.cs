using ZipStation.Business.Helpers;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface ITicketGateway
{
    Task<GatewayResponse> CanCreateTicketAsync(string companyId, string projectId);
    Task<GatewayResponse> CanGetTicketAsync(string companyId, string projectId);
    Task<GatewayResponse> CanUpdateTicketAsync(string companyId, string projectId);
    Task<GatewayResponse> CanDeleteTicketAsync(string companyId);
    Task<GatewayResponse> CanListTicketsAsync(string companyId);
    Task<GatewayResponse> CanAddMessageAsync(string companyId, string projectId);
}

public class TicketGateway : ITicketGateway
{
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public TicketGateway(IAppUser appUser, IPermissionService permissionService)
    {
        _appUser = appUser;
        _permissionService = permissionService;
    }

    public async Task<GatewayResponse> CanCreateTicketAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.TicketsCreate, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanGetTicketAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.TicketsView, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanUpdateTicketAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.TicketsEdit, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanDeleteTicketAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.TicketsDelete))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanListTicketsAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.TicketsView))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanAddMessageAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.TicketsEdit, projectId))
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
