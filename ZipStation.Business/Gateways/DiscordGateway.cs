using ZipStation.Business.Helpers;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface IDiscordGateway
{
    Task<GatewayResponse> CanViewAsync(string companyId, string projectId);
    Task<GatewayResponse> CanEditAsync(string companyId, string projectId);
    Task<GatewayResponse> CanSyncNowAsync(string companyId, string projectId);
}

public class DiscordGateway : IDiscordGateway
{
    private static readonly TimeSpan SyncNowCooldown = TimeSpan.FromSeconds(10);

    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;
    private readonly IRateLimiter _rateLimiter;

    public DiscordGateway(IAppUser appUser, IPermissionService permissionService, IRateLimiter rateLimiter)
    {
        _appUser = appUser;
        _permissionService = permissionService;
        _rateLimiter = rateLimiter;
    }

    public async Task<GatewayResponse> CanViewAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.DiscordView, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanEditAsync(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.DiscordEdit, projectId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanSyncNowAsync(string companyId, string projectId)
    {
        var editGate = await CanEditAsync(companyId, projectId);
        if (editGate.ResponseStatus != GatewayResponseCodes.Ok) return editGate;

        var key = $"discord:sync-now:{_appUser.UserId}:{projectId}";
        if (!_rateLimiter.TryAcquire(key, SyncNowCooldown, out var retryAfter))
        {
            return new GatewayResponse
            {
                ResponseStatus = GatewayResponseCodes.BadRequest,
                ResponseMessage = $"Please wait {Math.Ceiling(retryAfter.TotalSeconds)}s before triggering another sync."
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
