using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Responses;

namespace ZipStation.Business.Gateways;

public interface ICannedResponseGateway
{
    Task<GatewayResponse> CanListAsync(string companyId);
    Task<GatewayResponse> CanCreateAsync(string companyId);
    Task<GatewayResponse> CanUpdateAsync(string companyId);
    Task<GatewayResponse> CanDeleteAsync(string companyId);
}

public class CannedResponseGateway : ICannedResponseGateway
{
    private readonly IAppUser _appUser;
    private readonly IUserRepository _userRepository;

    public CannedResponseGateway(IAppUser appUser, IUserRepository userRepository)
    {
        _appUser = appUser;
        _userRepository = userRepository;
    }

    public async Task<GatewayResponse> CanListAsync(string companyId)
    {
        return await RequireCompanyMember(companyId);
    }

    public async Task<GatewayResponse> CanCreateAsync(string companyId)
    {
        return await RequireCompanyMember(companyId);
    }

    public async Task<GatewayResponse> CanUpdateAsync(string companyId)
    {
        return await RequireCompanyMember(companyId);
    }

    public async Task<GatewayResponse> CanDeleteAsync(string companyId)
    {
        return await RequireCompanyMember(companyId);
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

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
