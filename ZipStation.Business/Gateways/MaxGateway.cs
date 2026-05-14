using ZipStation.Business.Helpers;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface IMaxGateway
{
    Task<GatewayResponse> CanViewAsync(string companyId, string projectId);
    Task<GatewayResponse> CanEditAsync(string companyId, string projectId);
    Task<GatewayResponse> CanTestConnectionAsync(string companyId, string projectId);
}

public class MaxGateway : IMaxGateway
{
    private static readonly TimeSpan TestConnectionCooldown = TimeSpan.FromSeconds(5);

    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;
    private readonly IRateLimiter _rateLimiter;

    public MaxGateway(IAppUser appUser, IPermissionService permissionService, IRateLimiter rateLimiter)
    {
        _appUser = appUser;
        _permissionService = permissionService;
        _rateLimiter = rateLimiter;
    }

    public async Task<GatewayResponse> CanViewAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.MaxView, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanEditAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.MaxEdit, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanTestConnectionAsync(string companyId, string projectId)
    {
        var editGate = await CanEditAsync(companyId, projectId);
        if (editGate.ResponseStatus != GatewayResponseCodes.Ok) return editGate;

        var key = $"max:test-connection:{_appUser.UserId}:{projectId}";
        if (!_rateLimiter.TryAcquire(key, TestConnectionCooldown, out var retryAfter))
        {
            return new GatewayResponse
            {
                ResponseStatus = GatewayResponseCodes.BadRequest,
                ResponseMessage = $"Please wait {Math.Ceiling(retryAfter.TotalSeconds)}s before testing again."
            };
        }

        return Ok();
    }

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
