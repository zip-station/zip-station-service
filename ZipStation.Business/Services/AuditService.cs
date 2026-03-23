using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Services;

public interface IAuditService
{
    Task LogAsync(string companyId, string? projectId, string action, string entityType, string? entityId, IAppUser appUser, string? details = null);
}

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;

    public AuditService(IAuditLogRepository auditLogRepository, IUserRepository userRepository)
    {
        _auditLogRepository = auditLogRepository;
        _userRepository = userRepository;
    }

    public async Task LogAsync(string companyId, string? projectId, string action, string entityType, string? entityId, IAppUser appUser, string? details = null)
    {
        var displayName = "System";
        var userId = appUser.UserId ?? "system";

        if (!string.IsNullOrEmpty(appUser.UserId))
        {
            var user = await _userRepository.GetByFirebaseUserIdAsync(appUser.UserId);
            displayName = user?.DisplayName ?? appUser.Email ?? "Unknown";
            userId = user?.Id ?? appUser.UserId;
        }

        var entry = new AuditLogEntry
        {
            CompanyId = companyId,
            ProjectId = projectId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            UserDisplayName = displayName,
            Details = details
        };

        await _auditLogRepository.CreateAsync(entry);
    }
}
