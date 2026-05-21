using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ZipStation.Business.Services;

/// Thin Discord REST client for the API host — used by the settings picker to populate
/// server / channel dropdowns from a saved bot token. The worker has its own client
/// for polling; this one stays in the API project to keep dependencies isolated.
public interface IDiscordApiClient
{
    Task<DiscordApiResult<List<DiscordGuildInfo>>> ListBotGuildsAsync(string botToken, CancellationToken ct = default);
    Task<DiscordApiResult<List<DiscordChannelInfo>>> ListGuildChannelsAsync(string botToken, string guildId, CancellationToken ct = default);
}

public class DiscordApiResult<T>
{
    public bool Ok { get; set; }
    public T? Value { get; set; }
    /// Friendly error to surface in the SPA — already includes Discord's own message when available.
    public string? Error { get; set; }
    public int? StatusCode { get; set; }
}

public class DiscordGuildInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// Hash, not full URL — caller builds the CDN URL if it wants one.
    public string? Icon { get; set; }
}

public class DiscordChannelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// Discord channel-type integer. 0=text, 4=category, 5=announcement, 15=forum, 16=media.
    public int Type { get; set; }
    public string? ParentId { get; set; }
}

public class DiscordApiClient : IDiscordApiClient
{
    private const string DiscordApiBase = "https://discord.com/api/v10";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Discord uses snake_case (parent_id, owner_id, etc.). Case-insensitive alone won't bridge that.
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<DiscordApiClient> _logger;

    static DiscordApiClient()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ZipStationApi (https://zipstation.dev, 1.0)");
    }

    public DiscordApiClient(ILogger<DiscordApiClient> logger)
    {
        _logger = logger;
    }

    public async Task<DiscordApiResult<List<DiscordGuildInfo>>> ListBotGuildsAsync(string botToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(botToken))
            return new() { Ok = false, Error = "No bot token saved." };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{DiscordApiBase}/users/@me/guilds");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return await BuildErrorAsync<List<DiscordGuildInfo>>(response, "list guilds", ct);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var guilds = await JsonSerializer.DeserializeAsync<List<DiscordGuildInfo>>(stream, _json, ct);
            return new() { Ok = true, Value = guilds ?? new() };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Discord ListBotGuilds failed");
            return new() { Ok = false, Error = "Failed to reach Discord. Check the bot token and try again." };
        }
    }

    public async Task<DiscordApiResult<List<DiscordChannelInfo>>> ListGuildChannelsAsync(string botToken, string guildId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(botToken))
            return new() { Ok = false, Error = "No bot token saved." };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{DiscordApiBase}/guilds/{guildId}/channels");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return await BuildErrorAsync<List<DiscordChannelInfo>>(response, "list channels", ct);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var channels = await JsonSerializer.DeserializeAsync<List<DiscordChannelInfo>>(stream, _json, ct);
            return new() { Ok = true, Value = channels ?? new() };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Discord ListGuildChannels failed for {GuildId}", guildId);
            return new() { Ok = false, Error = "Failed to reach Discord. Check the bot token and try again." };
        }
    }

    private static async Task<DiscordApiResult<T>> BuildErrorAsync<T>(HttpResponseMessage response, string action, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        string? message = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString();
        }
        catch { /* body wasn't JSON; fall through */ }

        var friendly = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Bot token is invalid or has been reset.",
            System.Net.HttpStatusCode.Forbidden => "Bot doesn't have access. Make sure it's been invited to the server.",
            System.Net.HttpStatusCode.NotFound => "Server not found, or bot isn't in it.",
            _ => $"Discord refused the request ({(int)response.StatusCode}): {message ?? action + " failed"}"
        };

        return new() { Ok = false, Error = friendly, StatusCode = (int)response.StatusCode };
    }
}
