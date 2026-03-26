using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.Constants;
using ZipStation.Models.Entities;


namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system")]
public class SystemController : BaseController
{
    private readonly ILogger<SystemController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAppUser _appUser;
    private readonly IMongoDatabase _database;
    private readonly AppConfig _appConfig;
    private readonly IPermissionService _permissionService;

    public SystemController(
        ILogger<SystemController> logger,
        IUserRepository userRepository,
        ICompanyRepository companyRepository,
        IAppUser appUser,
        IMongoDatabase database,
        IOptions<AppConfig> appConfig,
        IPermissionService permissionService)
    {
        _logger = logger;
        _userRepository = userRepository;
        _companyRepository = companyRepository;
        _appUser = appUser;
        _database = database;
        _appConfig = appConfig.Value;
        _permissionService = permissionService;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus()
    {
        var count = await _userRepository.CountAsync();
        return Ok(new { initialized = count > 0 });
    }

    [HttpPost("setup")]
    [Authorize]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request)
    {
        var existingCount = await _userRepository.CountAsync();
        if (existingCount > 0)
        {
            return BadRequest(new { message = "System is already initialized." });
        }

        var firebaseUserId = _appUser.UserId;
        var email = _appUser.Email;

        if (string.IsNullOrEmpty(firebaseUserId) || string.IsNullOrEmpty(email))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            return BadRequest(new { message = "Company name is required." });
        }

        var slug = request.CompanyName
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace("'", "");

        var company = new Company
        {
            Name = request.CompanyName,
            Slug = slug,
            OwnerUserId = string.Empty,
            Settings = new CompanySettings()
        };

        var createdCompany = await _companyRepository.CreateAsync(company);

        // Check if a pre-created user record exists from an invite
        var invited = await _userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant());
        User createdUser;
        if (invited != null && string.IsNullOrEmpty(invited.FirebaseUserId))
        {
            invited.FirebaseUserId = firebaseUserId;
            invited.DisplayName = request.DisplayName ?? email.Split('@')[0];
            invited.RoleAssignments ??= new List<RoleAssignment>();
            createdUser = await _userRepository.UpdateAsync(invited);
        }
        else
        {
            var user = new User
            {
                FirebaseUserId = firebaseUserId,
                Email = email,
                DisplayName = request.DisplayName ?? email.Split('@')[0],
                RoleAssignments = new List<RoleAssignment>()
            };

            createdUser = await _userRepository.CreateAsync(user);
        }

        createdCompany.OwnerUserId = createdUser.Id;
        await _companyRepository.UpdateAsync(createdCompany);

        // Add a company-wide role assignment so the owner shows up in company member queries
        createdUser.RoleAssignments.Add(new RoleAssignment
        {
            CompanyId = createdCompany.Id,
            RoleId = string.Empty,
            ProjectId = null
        });
        await _userRepository.UpdateAsync(createdUser);

        _logger.LogInformation("System initialized by {Email} — Company: {CompanyName}", email, request.CompanyName);

        return Ok(new
        {
            userId = createdUser.Id,
            companyId = createdCompany.Id,
            companyName = createdCompany.Name
        });
    }
    [HttpPost("trigger-poll")]
    [Authorize]
    public async Task<IActionResult> TriggerPoll()
    {
        var company = await _companyRepository.GetFirstAsync();
        if (company != null && !await _permissionService.HasPermissionAsync(_appUser.UserId!, company.Id, Permissions.IntakeCheckNow))
            return StatusCode(403, new { message = "Insufficient permissions" });

        var collection = _database.GetCollection<BsonDocument>(_appConfig.ZipStationMongoDb.Collections.WorkerTriggers);
        await collection.InsertOneAsync(new BsonDocument
        {
            { "type", "poll-emails" },
            { "createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        });
        return Ok(new { triggered = true });
    }

    [HttpPost("import-history")]
    [Authorize]
    public async Task<IActionResult> TriggerHistoryImport()
    {
        var company = await _companyRepository.GetFirstAsync();
        if (company != null && !await _permissionService.HasPermissionAsync(_appUser.UserId!, company.Id, Permissions.IntakeImportHistory))
            return StatusCode(403, new { message = "Insufficient permissions" });

        var collection = _database.GetCollection<BsonDocument>(_appConfig.ZipStationMongoDb.Collections.WorkerTriggers);
        await collection.InsertOneAsync(new BsonDocument
        {
            { "type", "import-history" },
            { "createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        });
        return Ok(new { triggered = true });
    }

    [HttpGet("poll-status")]
    [Authorize]
    public async Task<IActionResult> GetPollStatus()
    {
        var collection = _database.GetCollection<BsonDocument>(_appConfig.ZipStationMongoDb.Collections.WorkerTriggers);
        // Check if there's a pending trigger
        var pending = await collection.Find(new BsonDocument("type", "poll-emails")).AnyAsync();
        // Get last poll time from the most recent "poll-complete" record
        var lastPoll = await collection.Find(new BsonDocument("type", "poll-complete"))
            .SortByDescending(d => d["completedAt"])
            .Limit(1)
            .FirstOrDefaultAsync();
        var lastPollTime = lastPoll?["completedAt"]?.AsInt64 ?? 0;

        return Ok(new { pending, lastPollTime, pollIntervalSeconds = 120 });
    }
}

public class SetupRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
