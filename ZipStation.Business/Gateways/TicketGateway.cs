using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Enums;
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
    private readonly IUserRepository _userRepository;

    public TicketGateway(IAppUser appUser, IUserRepository userRepository)
    {
        _appUser = appUser;
        _userRepository = userRepository;
    }

    public async Task<GatewayResponse> CanCreateTicketAsync(string companyId, string projectId)
    {
        return await RequireProjectAccessOrCompanyMember(companyId, projectId);
    }

    public async Task<GatewayResponse> CanGetTicketAsync(string companyId, string projectId)
    {
        return await RequireProjectAccessOrCompanyMember(companyId, projectId);
    }

    public async Task<GatewayResponse> CanUpdateTicketAsync(string companyId, string projectId)
    {
        return await RequireProjectAccessOrCompanyMember(companyId, projectId);
    }

    public async Task<GatewayResponse> CanDeleteTicketAsync(string companyId)
    {
        return await RequireCompanyRole(companyId, CompanyRole.Admin);
    }

    public async Task<GatewayResponse> CanListTicketsAsync(string companyId)
    {
        return await RequireCompanyMember(companyId);
    }

    public async Task<GatewayResponse> CanAddMessageAsync(string companyId, string projectId)
    {
        return await RequireProjectAccessOrCompanyMember(companyId, projectId);
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

    private async Task<GatewayResponse> RequireProjectAccessOrCompanyMember(string companyId, string projectId)
    {
        if (!_appUser.IsAuthenticated || string.IsNullOrEmpty(_appUser.UserId))
            return Unauthorized();

        var user = await _userRepository.GetByFirebaseUserIdAsync(_appUser.UserId);
        if (user == null) return Unauthorized("User not found");

        var isCompanyMember = user.CompanyMemberships.Any(m => m.CompanyId == companyId);
        if (!isCompanyMember) return Unauthorized("You are not a member of this company");

        return Ok();
    }

    private static GatewayResponse Ok() => new() { ResponseStatus = GatewayResponseCodes.Ok };
    private static GatewayResponse Unauthorized(string? msg = null) => new()
    {
        ResponseStatus = GatewayResponseCodes.Unauthorized,
        ResponseMessage = msg ?? "Unauthorized"
    };
}
