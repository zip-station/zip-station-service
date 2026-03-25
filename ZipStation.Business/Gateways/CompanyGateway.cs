using ZipStation.Business.Helpers;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface ICompanyGateway
{
    Task<GatewayResponse> CanCreateCompanyAsync();
    Task<GatewayResponse> CanGetCompanyAsync(string companyId);
    Task<GatewayResponse> CanReplaceCompanyAsync(string companyId);
    Task<GatewayResponse> CanUpdateCompanyAsync(string companyId);
    Task<GatewayResponse> CanDeleteCompanyAsync(string companyId);
    Task<GatewayResponse> CanListCompaniesAsync();
}

public class CompanyGateway : ICompanyGateway
{
    private readonly IAppUser _appUser;
    private readonly IPermissionService _permissionService;

    public CompanyGateway(IAppUser appUser, IPermissionService permissionService)
    {
        _appUser = appUser;
        _permissionService = permissionService;
    }

    public async Task<GatewayResponse> CanCreateCompanyAsync()
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        return Ok();
    }

    public async Task<GatewayResponse> CanGetCompanyAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        // Any permission check will confirm the user has a role in this company
        if (!await _permissionService.HasPermissionAsync(_appUser.UserId, companyId, Permissions.DashboardView))
            return Unauthorized("You are not a member of this company");

        return Ok();
    }

    public async Task<GatewayResponse> CanReplaceCompanyAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.IsOwnerAsync(_appUser.UserId, companyId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanUpdateCompanyAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.IsOwnerAsync(_appUser.UserId, companyId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanDeleteCompanyAsync(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        if (!await _permissionService.IsOwnerAsync(_appUser.UserId, companyId))
            return Unauthorized("Insufficient permissions");

        return Ok();
    }

    public async Task<GatewayResponse> CanListCompaniesAsync()
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        return Ok();
    }

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
