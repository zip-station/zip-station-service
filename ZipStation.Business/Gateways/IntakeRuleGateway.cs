using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Enums;
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
    private readonly IUserRepository _userRepository;

    public IntakeRuleGateway(IAppUser appUser, IUserRepository userRepository)
    {
        _appUser = appUser;
        _userRepository = userRepository;
    }

    public async Task<GatewayResponse> CanListRulesAsync(string companyId)
    {
        return await RequireCompanyMember(companyId);
    }

    public async Task<GatewayResponse> CanCreateRuleAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    public async Task<GatewayResponse> CanUpdateRuleAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    public async Task<GatewayResponse> CanDeleteRuleAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    private async Task<GatewayResponse> RequireCompanyMember(string companyId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var user = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId);
        if (user == null) return Unauthorized("User not found");

        if (!user.CompanyMemberships.Any(m => m.CompanyId == companyId))
            return Unauthorized("You are not a member of this company");

        return Ok();
    }

    private async Task<GatewayResponse> RequireCompanyRole(string companyId, CompanyRole minimumRole)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var user = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId);
        if (user == null) return Unauthorized("User not found");

        var membership = user.CompanyMemberships.FirstOrDefault(m => m.CompanyId == companyId);
        if (membership == null) return Unauthorized("You are not a member of this company");
        if ((int)membership.Role > (int)minimumRole) return Unauthorized($"Requires {minimumRole} role or higher");

        return Ok();
    }

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
