using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Enums;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface ICustomerGateway
{
    Task<GatewayResponse> CanListCustomersAsync(string companyId);
    Task<GatewayResponse> CanGetCustomerAsync(string companyId);
    Task<GatewayResponse> CanCreateCustomerAsync(string companyId, string projectId);
    Task<GatewayResponse> CanUpdateCustomerAsync(string companyId);
    Task<GatewayResponse> CanDeleteCustomerAsync(string companyId);
}

public class CustomerGateway : ICustomerGateway
{
    private readonly IAppUser _appUser;
    private readonly IUserRepository _userRepository;

    public CustomerGateway(IAppUser appUser, IUserRepository userRepository)
    {
        _appUser = appUser;
        _userRepository = userRepository;
    }

    public async Task<GatewayResponse> CanListCustomersAsync(string companyId)
    {
        return await RequireCompanyMember(companyId);
    }

    public async Task<GatewayResponse> CanGetCustomerAsync(string companyId)
    {
        return await RequireCompanyMember(companyId);
    }

    public async Task<GatewayResponse> CanCreateCustomerAsync(string companyId, string projectId)
    {
        return await RequireCompanyMember(companyId);
    }

    public async Task<GatewayResponse> CanUpdateCustomerAsync(string companyId)
    {
        return await RequireCompanyMember(companyId);
    }

    public async Task<GatewayResponse> CanDeleteCustomerAsync(string companyId)
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
