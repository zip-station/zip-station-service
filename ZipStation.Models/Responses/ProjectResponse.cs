using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class ProjectResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string SupportEmailAddress { get; set; } = string.Empty;
    public ProjectSettingsResponse Settings { get; set; } = new();
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}

public class ProjectSettingsResponse
{
    public AssignmentMode AssignmentMode { get; set; }
    public string? DefaultLanguage { get; set; }
    public SmtpSettingsResponse? Smtp { get; set; }
    public ImapSettingsResponse? Imap { get; set; }
    public TicketIdSettingsResponse TicketId { get; set; } = new();
    public bool AllowUserLanguageOverride { get; set; }
    public int StaleTicketDays { get; set; } = 5;
    public EmailSignatureSettingsResponse? EmailSignature { get; set; }
    public AutoReplySettingsResponse? AutoReply { get; set; }
    public SpamSettingsResponse? Spam { get; set; }
    public ContactFormSettingsResponse? ContactForm { get; set; }
}

public class EmailSignatureSettingsResponse
{
    public bool Enabled { get; set; }
    public string SignatureHtml { get; set; } = string.Empty;
    public bool AllowUserOverride { get; set; }
}

public class AutoReplySettingsResponse
{
    public bool Enabled { get; set; }
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
}

public class SpamSettingsResponse
{
    public int AutoDenyThreshold { get; set; } = 80;
    public int FlagThreshold { get; set; } = 50;
    public bool AutoDenyEnabled { get; set; }
}

public class ContactFormSettingsResponse
{
    public bool Enabled { get; set; }
    public List<string> SystemSenderEmails { get; set; } = new();
    public string EmailLabel { get; set; } = "Email";
    public string NameLabel { get; set; } = "Name";
    public string MessageLabel { get; set; } = "Message";
    public string? SubjectLabel { get; set; }
}

public class SmtpSettingsResponse
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
    public bool HasPassword { get; set; }
}

public class ImapSettingsResponse
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
    public bool HasPassword { get; set; }
}

public class TicketIdSettingsResponse
{
    public string Prefix { get; set; } = string.Empty;
    public int MinLength { get; set; } = 3;
    public int MaxLength { get; set; } = 6;
    public TicketIdFormat Format { get; set; } = TicketIdFormat.Numeric;
    public string SubjectTemplate { get; set; } = "{ProjectName} - Ticket {TicketId}";
}
