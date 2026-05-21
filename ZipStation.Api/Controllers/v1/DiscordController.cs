using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies/{companyId}/projects/{projectId}/discord")]
[Authorize]
public class DiscordController : BaseController
{
    private readonly ILogger<DiscordController> _logger;
    private readonly IDiscordGateway _discordGateway;
    private readonly IProjectRepository _projectRepository;
    private readonly IAuditService _auditService;
    private readonly IAppUser _appUser;
    private readonly IMapper _mapper;
    private readonly IMongoDatabase _database;
    private readonly AppConfig _appConfig;
    private readonly IDiscordApiClient _discordApi;

    public DiscordController(
        ILogger<DiscordController> logger,
        IDiscordGateway discordGateway,
        IProjectRepository projectRepository,
        IAuditService auditService,
        IAppUser appUser,
        IMapper mapper,
        IMongoDatabase database,
        IOptions<AppConfig> appConfig,
        IDiscordApiClient discordApi)
    {
        _logger = logger;
        _discordGateway = discordGateway;
        _projectRepository = projectRepository;
        _auditService = auditService;
        _appUser = appUser;
        _mapper = mapper;
        _database = database;
        _appConfig = appConfig.Value;
        _discordApi = discordApi;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DiscordSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _discordGateway.CanViewAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            var settings = project.Settings?.Discord ?? new DiscordSettings();
            return Ok(_mapper.Map<DiscordSettingsResponse>(settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Discord settings for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("bot-token")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DiscordSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetBotToken(string companyId, string projectId, [FromBody] DiscordBotTokenCommandModel commandModel)
    {
        try
        {
            var gatewayResponse = await _discordGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            project.Settings ??= new ProjectSettings();
            project.Settings.Discord ??= new DiscordSettings();

            if (string.IsNullOrEmpty(commandModel.BotToken))
            {
                project.Settings.Discord.BotTokenEncrypted = string.Empty;
                project.Settings.Discord.Enabled = false;
            }
            else
            {
                project.Settings.Discord.BotTokenEncrypted = EncryptionHelper.Encrypt(commandModel.BotToken);
            }

            await _projectRepository.UpdateAsync(project);
            await _auditService.LogAsync(companyId, projectId, "DiscordBotTokenUpdated", "Project", projectId, _appUser);

            return Ok(_mapper.Map<DiscordSettingsResponse>(project.Settings.Discord));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Discord bot token for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("enabled")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DiscordSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetEnabled(string companyId, string projectId, [FromBody] DiscordEnabledCommandModel commandModel)
    {
        try
        {
            var gatewayResponse = await _discordGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            project.Settings ??= new ProjectSettings();
            project.Settings.Discord ??= new DiscordSettings();

            if (commandModel.Enabled && string.IsNullOrEmpty(project.Settings.Discord.BotTokenEncrypted))
                return BadRequest(new BadRequestResponse { Message = "Set a bot token before enabling Discord intake." });

            project.Settings.Discord.Enabled = commandModel.Enabled;
            await _projectRepository.UpdateAsync(project);

            return Ok(_mapper.Map<DiscordSettingsResponse>(project.Settings.Discord));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling Discord enabled for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("sources")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DiscordSourceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddSource(string companyId, string projectId, [FromBody] DiscordSourceCommandModel commandModel)
    {
        try
        {
            var gatewayResponse = await _discordGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            if (string.IsNullOrWhiteSpace(commandModel.GuildId) || string.IsNullOrWhiteSpace(commandModel.ChannelId))
                return BadRequest(new BadRequestResponse { Message = "GuildId and ChannelId are required." });

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            project.Settings ??= new ProjectSettings();
            project.Settings.Discord ??= new DiscordSettings();

            if (project.Settings.Discord.Sources.Any(s => s.GuildId == commandModel.GuildId && s.ChannelId == commandModel.ChannelId))
                return BadRequest(new BadRequestResponse { Message = "A source already exists for that guild and channel." });

            var source = new DiscordSource
            {
                Name = commandModel.Name,
                GuildId = commandModel.GuildId,
                ChannelId = commandModel.ChannelId,
                IsForum = commandModel.IsForum,
                DefaultCardType = commandModel.DefaultCardType,
                Enabled = commandModel.Enabled
            };
            project.Settings.Discord.Sources.Add(source);

            await _projectRepository.UpdateAsync(project);
            await _auditService.LogAsync(companyId, projectId, "DiscordSourceAdded", "Project", projectId, _appUser, source.Name);

            return Ok(_mapper.Map<DiscordSourceResponse>(source));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding Discord source for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPut("sources/{sourceId}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DiscordSourceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSource(string companyId, string projectId, string sourceId, [FromBody] DiscordSourceCommandModel commandModel)
    {
        try
        {
            var gatewayResponse = await _discordGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            var source = project.Settings?.Discord?.Sources.FirstOrDefault(s => s.Id == sourceId);
            if (source == null) return NotFound();

            source.Name = commandModel.Name;
            source.GuildId = commandModel.GuildId;
            source.ChannelId = commandModel.ChannelId;
            source.IsForum = commandModel.IsForum;
            source.DefaultCardType = commandModel.DefaultCardType;
            source.Enabled = commandModel.Enabled;

            await _projectRepository.UpdateAsync(project);
            return Ok(_mapper.Map<DiscordSourceResponse>(source));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Discord source {SourceId}", sourceId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpDelete("sources/{sourceId}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteSource(string companyId, string projectId, string sourceId)
    {
        try
        {
            var gatewayResponse = await _discordGateway.CanEditAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            var source = project.Settings?.Discord?.Sources.FirstOrDefault(s => s.Id == sourceId);
            if (source == null) return NotFound();

            project.Settings!.Discord!.Sources.Remove(source);
            await _projectRepository.UpdateAsync(project);
            await _auditService.LogAsync(companyId, projectId, "DiscordSourceRemoved", "Project", projectId, _appUser, source.Name);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Discord source {SourceId}", sourceId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("guilds")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<DiscordGuildSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListGuilds(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _discordGateway.CanViewAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            var token = EncryptionHelper.Decrypt(project.Settings?.Discord?.BotTokenEncrypted ?? "");
            if (string.IsNullOrEmpty(token))
                return BadRequest(new BadRequestResponse { Message = "Save a bot token before listing servers." });

            var result = await _discordApi.ListBotGuildsAsync(token);
            if (!result.Ok || result.Value == null)
                return BadRequest(new BadRequestResponse { Message = result.Error ?? "Failed to list servers." });

            var response = result.Value.Select(g => new DiscordGuildSummaryResponse
            {
                Id = g.Id,
                Name = g.Name,
                IconUrl = string.IsNullOrEmpty(g.Icon) ? null : $"https://cdn.discordapp.com/icons/{g.Id}/{g.Icon}.png",
            }).OrderBy(g => g.Name).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Discord guilds for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpGet("guilds/{guildId}/channels")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(List<DiscordChannelSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListGuildChannels(string companyId, string projectId, string guildId, [FromQuery] bool forumOnly = true)
    {
        try
        {
            var gatewayResponse = await _discordGateway.CanViewAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            var token = EncryptionHelper.Decrypt(project.Settings?.Discord?.BotTokenEncrypted ?? "");
            if (string.IsNullOrEmpty(token))
                return BadRequest(new BadRequestResponse { Message = "Save a bot token before listing channels." });

            var result = await _discordApi.ListGuildChannelsAsync(token, guildId);
            if (!result.Ok || result.Value == null)
                return BadRequest(new BadRequestResponse { Message = result.Error ?? "Failed to list channels." });

            // Channel type 15 = GUILD_FORUM. Default to forum-only since that's what intake watches today.
            const int ForumType = 15;
            var filtered = forumOnly
                ? result.Value.Where(c => c.Type == ForumType).ToList()
                : result.Value;

            var response = filtered.Select(c => new DiscordChannelSummaryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Type = c.Type,
                ParentId = c.ParentId,
                IsForum = c.Type == ForumType,
            }).OrderBy(c => c.Name).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Discord channels for project {ProjectId} guild {GuildId}", projectId, guildId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }

    [HttpPost("sync-now")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncNow(string companyId, string projectId)
    {
        try
        {
            var gatewayResponse = await _discordGateway.CanSyncNowAsync(companyId, projectId);
            if (gatewayResponse.ResponseStatus != GatewayResponseCodes.Ok)
                return ProcessGatewayResponse(gatewayResponse);

            var project = await _projectRepository.GetAsync(projectId);
            if (project == null || project.CompanyId != companyId) return NotFound();

            var collection = _database.GetCollection<BsonDocument>(_appConfig.ZipStationMongoDb.Collections.WorkerTriggers);
            await collection.InsertOneAsync(new BsonDocument
            {
                { "type", "poll-discord" },
                { "projectId", projectId },
                { "createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
            });

            await _auditService.LogAsync(companyId, projectId, "DiscordSyncRequested", "Project", projectId, _appUser);
            return Ok(new { triggered = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering Discord sync for project {ProjectId}", projectId);
            return StatusCode(500, new BadRequestResponse { Message = "An unexpected error occurred" });
        }
    }
}
