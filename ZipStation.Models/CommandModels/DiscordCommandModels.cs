using ZipStation.Models.Enums;

namespace ZipStation.Models.CommandModels;

public class DiscordBotTokenCommandModel
{
    public string BotToken { get; set; } = string.Empty;
}

public class DiscordEnabledCommandModel
{
    public bool Enabled { get; set; }
}

public class DiscordSourceCommandModel
{
    public string Name { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public bool IsForum { get; set; } = true;
    /// Null = "Auto — let Max decide". Otherwise an explicit KanbanCardType.
    public KanbanCardType? DefaultCardType { get; set; }
    public bool Enabled { get; set; } = true;
}
