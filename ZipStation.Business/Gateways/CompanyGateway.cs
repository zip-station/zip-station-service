using Microsoft.Extensions.Options;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Enums;
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
    private readonly IUserRepository _userRepository;

    public CompanyGateway(IAppUser appUser, IUserRepository userRepository)
    {
        _appUser = appUser;
        _userRepository = userRepository;
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

        var user = await GetCurrentUser();
        if (user == null) return Unauthorized("User not found");

        if (!HasCompanyMembership(user, companyId))
            return Unauthorized("You are not a member of this company");

        return Ok();
    }

    public async Task<GatewayResponse> CanReplaceCompanyAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    public async Task<GatewayResponse> CanUpdateCompanyAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    public async Task<GatewayResponse> CanDeleteCompanyAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Owner);
    }

    public async Task<GatewayResponse> CanListCompaniesAsync()
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        return Ok();
    }

    private async Task<GatewayResponse> RequireCompanyRole(string companyId, CompanyRole minimumRole)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var user = await GetCurrentUser();
        if (user == null) return Unauthorized("User not found");

        var membership = user.CompanyMemberships.FirstOrDefault(m => m.CompanyId == companyId);
        if (membership == null)
            return Unauthorized("You are not a member of this company");

        if (!MeetsMinimumRole(membership.Role, minimumRole))
            return Unauthorized($"Requires {minimumRole} role or higher");

        return Ok();
    }

    private async Task<Models.Entities.User?> GetCurrentUser()
    {
        return await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId!);
    }

    private static bool HasCompanyMembership(Models.Entities.User user, string companyId)
    {
        return user.CompanyMemberships.Any(m => m.CompanyId == companyId);
    }

    private static bool MeetsMinimumRole(CompanyRole actual, CompanyRole required)
    {
        // Owner(0) > Admin(1) > Member(2) — lower enum value = higher privilege
        return (int)actual <= (int)required;
    }

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
