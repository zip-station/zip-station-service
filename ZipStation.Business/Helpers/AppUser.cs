using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ZipStation.Business.Helpers;

public interface IAppUser
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    string? JwtToken { get; }
}

public class AppUser : IAppUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId
    {
        get
        {
            try
            {
                return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? _httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value;
            }
            catch { return null; }
        }
    }

    public string? Email
    {
        get
        {
            try
            {
                return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
                    ?? _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value;
            }
            catch { return null; }
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            try
            {
                return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
            }
            catch { return false; }
        }
    }

    public string? JwtToken
    {
        get
        {
            try
            {
                var authHeader = _httpContextAccessor.HttpContext?.Request?.Headers["Authorization"].FirstOrDefault();
                return authHeader?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
            }
            catch { return null; }
        }
    }
}
