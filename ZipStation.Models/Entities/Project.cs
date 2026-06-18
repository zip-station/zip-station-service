using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;
using ZipStation.Models.Serialization;

namespace ZipStation.Models.Entities;

public class Project : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Name { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? LogoUrl { get; set; }

    [DoNotClearOnPatch]
    public string SupportEmailAddress { get; set; } = string.Empty;

    public ProjectSettings Settings { get; set; } = new();
}

public class ProjectSettings
{
    public AssignmentMode AssignmentMode { get; set; } = AssignmentMode.Manual;

    public string? DefaultLanguage { get; set; }

    public SmtpSettings? Smtp { get; set; }

    public ImapSettings? Imap { get; set; }

    public TicketIdSettings TicketId { get; set; } = new();

    public bool AllowUserLanguageOverride { get; set; } = true;

    public int StaleTicketDays { get; set; } = 5;

    public int KanbanArchiveDays { get; set; } = 3;

    public EmailSignatureSettings? EmailSignature { get; set; }

    public AutoReplySettings? AutoReply { get; set; }

    public SpamSettings? Spam { get; set; }

    public ContactFormSettings? ContactForm { get; set; }

    public FileStorageSettings? FileStorage { get; set; }

    public MaxSettings? Max { get; set; }

    public DiscordSettings? Discord { get; set; }
}

public class DiscordSettings
{
    public bool Enabled { get; set; }

    public string BotTokenEncrypted { get; set; } = string.Empty;

    public List<DiscordSource> Sources { get; set; } = new();
}

public class DiscordSource
{
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

    public string Name { get; set; } = string.Empty;

    public string GuildId { get; set; } = string.Empty;

    public string ChannelId { get; set; } = string.Empty;

    public bool IsForum { get; set; } = true;

    /// Highest snowflake ID already turned into a story. Cursors forward as new threads/messages arrive.
    public string? LastSeenId { get; set; }

    /// Default card type for cards created from this source. Null means "let Max decide".
    /// When Max is disabled or its call fails the worker falls back to Bug. A built-in type
    /// name or a custom type id from the project's board. The serializer tolerates legacy int
    /// values stored before story types became strings.
    [BsonIgnoreIfNull]
    [BsonSerializer(typeof(LegacyCardTypeStringSerializer))]
    public string? DefaultCardType { get; set; }

    public bool Enabled { get; set; } = true;
}

public class MaxSettings
{
    public bool Enabled { get; set; }

    public string ApiKeyEncrypted { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-sonnet-4-6";

    public string? ProjectContext { get; set; }

    public string? ToneGuide { get; set; }

    public string? ToneAvoid { get; set; }

    public bool AutoSendEnabled { get; set; }

    public double AutoSendThreshold { get; set; } = 0.95;

    public List<string> AutoSendCategories { get; set; } = new() { "billing" };
}

public class EmailSignatureSettings
{
    public bool Enabled { get; set; }

    public string SignatureHtml { get; set; } = string.Empty;

    public bool AllowUserOverride { get; set; } = true;
}

public class AutoReplySettings
{
    public bool Enabled { get; set; }

    public string SubjectTemplate { get; set; } = "Re: {TicketSubject}";

    public string BodyTemplate { get; set; } = "<p>Hi {CustomerName},</p><p>We've received your message and created ticket <strong>{TicketId}</strong>. Our team will get back to you shortly.</p><p>Thanks,<br/>{ProjectName} Support</p>";
}

public class SpamSettings
{
    public int AutoDenyThreshold { get; set; } = 80;

    public int FlagThreshold { get; set; } = 50;

    public bool AutoDenyEnabled { get; set; }
}

public class ContactFormSettings
{
    public bool Enabled { get; set; }

    public List<string> SystemSenderEmails { get; set; } = new();

    public string EmailLabel { get; set; } = "Email";

    public string NameLabel { get; set; } = "Name";

    public string MessageLabel { get; set; } = "Message";

    public string? SubjectLabel { get; set; }
}

public class FileStorageSettings
{
    public string KeyId { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
}

public class ImapSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
}

public class TicketIdSettings
{
    public string Prefix { get; set; } = string.Empty;

    public int MinLength { get; set; } = 3;

    public int MaxLength { get; set; } = 6;

    public TicketIdFormat Format { get; set; } = TicketIdFormat.Numeric;

    public string SubjectTemplate { get; set; } = "{ProjectName} - Ticket {TicketId}";

    public long StartingNumber { get; set; }

    public bool UseRandomNumbers { get; set; }
}
