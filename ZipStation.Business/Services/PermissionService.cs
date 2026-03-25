using ZipStation.Business.Repositories;
using ZipStation.Models.Constants;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Services;

public interface IPermissionService
{
    /// <summary>
    /// Check if a user has a specific permission, either company-wide or for a specific project.
    /// Owners always return true.
    /// </summary>
    Task<bool> HasPermissionAsync(string userId, string companyId, string permission, string? projectId = null);

    /// <summary>
    /// Get all effective permissions for a user in a company (optionally scoped to a project).
    /// </summary>
    Task<HashSet<string>> GetEffectivePermissionsAsync(string userId, string companyId, string? projectId = null);

    /// <summary>
    /// Check if the user is the company owner.
    /// </summary>
    Task<bool> IsOwnerAsync(string userId, string companyId);

    /// <summary>
    /// Get all project IDs a user has access to in a company.
    /// Owners get all projects.
    /// </summary>
    Task<List<string>> GetAccessibleProjectIdsAsync(string userId, string companyId);
}

public class PermissionService : IPermissionService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IProjectRepository _projectRepository;

    public PermissionService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ICompanyRepository companyRepository,
        IProjectRepository projectRepository)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _companyRepository = companyRepository;
        _projectRepository = projectRepository;
    }

    public async Task<bool> IsOwnerAsync(string userId, string companyId)
    {
        var company = await _companyRepository.GetAsync(companyId);
        if (company == null) return false;

        var user = await _userRepository.GetByFirebaseUserIdAsync(userId);
        return user != null && company.OwnerUserId == user.Id;
    }

    public async Task<bool> HasPermissionAsync(string userId, string companyId, string permission, string? projectId = null)
    {
        if (await IsOwnerAsync(userId, companyId)) return true;

        var permissions = await GetEffectivePermissionsAsync(userId, companyId, projectId);
        return permissions.Contains(permission);
    }

    public async Task<HashSet<string>> GetEffectivePermissionsAsync(string userId, string companyId, string? projectId = null)
    {
        var user = await _userRepository.GetByFirebaseUserIdAsync(userId);
        if (user == null) return new HashSet<string>();

        // Check if owner — gets all permissions
        var company = await _companyRepository.GetAsync(companyId);
        if (company != null && company.OwnerUserId == user.Id)
            return new HashSet<string>(Permissions.All);

        // Collect role IDs from matching assignments
        var matchingRoleIds = user.RoleAssignments
            .Where(ra => ra.CompanyId == companyId &&
                         (ra.ProjectId == null || ra.ProjectId == projectId)) // company-wide + project-specific
            .Select(ra => ra.RoleId)
            .Distinct()
            .ToList();

        if (matchingRoleIds.Count == 0) return new HashSet<string>();

        // Fetch roles and merge permissions
        var roles = await _roleRepository.GetByIdsAsync(matchingRoleIds);
        var permissions = new HashSet<string>();
        foreach (var role in roles)
        {
            foreach (var perm in role.Permissions)
                permissions.Add(perm);
        }

        return permissions;
    }

    public async Task<List<string>> GetAccessibleProjectIdsAsync(string userId, string companyId)
    {
        var user = await _userRepository.GetByFirebaseUserIdAsync(userId);
        if (user == null) return new List<string>();

        // Owner gets all projects
        var company = await _companyRepository.GetAsync(companyId);
        if (company != null && company.OwnerUserId == user.Id)
        {
            var allProjects = await _projectRepository.GetByCompanyIdAsync(companyId);
            return allProjects.Select(p => p.Id).ToList();
        }

        // Company-wide assignments → all projects
        var hasCompanyWide = user.RoleAssignments.Any(ra => ra.CompanyId == companyId && ra.ProjectId == null);
        if (hasCompanyWide)
        {
            var allProjects = await _projectRepository.GetByCompanyIdAsync(companyId);
            return allProjects.Select(p => p.Id).ToList();
        }

        // Project-specific assignments
        return user.RoleAssignments
            .Where(ra => ra.CompanyId == companyId && ra.ProjectId != null)
            .Select(ra => ra.ProjectId!)
            .Distinct()
            .ToList();
    }
}
