using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ZipStation.Business.Repositories;

namespace ZipStation.Api.Helpers;

public class PatAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Pat";
    public const string TokenPrefix = "zs_pat_";

    private readonly IPersonalAccessTokenRepository _patRepository;
    private readonly IUserRepository _userRepository;

    public PatAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IPersonalAccessTokenRepository patRepository,
        IUserRepository userRepository)
        : base(options, logger, encoder)
    {
        _patRepository = patRepository;
        _userRepository = userRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        var rawToken = authHeader["Bearer ".Length..].Trim();
        if (!rawToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var pat = await _patRepository.GetByTokenHashAsync(tokenHash);
        if (pat == null)
            return AuthenticateResult.Fail("Invalid personal access token");

        if (pat.ExpiresOnDateTime.HasValue &&
            pat.ExpiresOnDateTime.Value < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            return AuthenticateResult.Fail("Personal access token has expired");

        var user = await _userRepository.GetAsync(pat.UserId);
        if (user == null || user.IsVoid || user.IsDisabled)
            return AuthenticateResult.Fail("Token owner is disabled or no longer exists");

        // Touch last-used asynchronously — don't block auth on the write.
        _ = Task.Run(async () =>
        {
            try { await _patRepository.TouchLastUsedAsync(pat.Id); }
            catch (Exception ex) { Logger.LogWarning(ex, "Failed to update PAT last-used timestamp"); }
        });

        // Build a ClaimsPrincipal that mirrors what the Firebase JWT bearer handler produces,
        // so IAppUser.UserId returns the FirebaseUserId — controllers/gateways work unchanged.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.FirebaseUserId),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("user_id", user.FirebaseUserId),
            new Claim("pat_id", pat.Id),
            new Claim("pat_company_id", pat.CompanyId),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
