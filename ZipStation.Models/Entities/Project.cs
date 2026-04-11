using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;

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

    public EmailSignatureSettings? EmailSignature { get; set; }

    public AutoReplySettings? AutoReply { get; set; }

    public SpamSettings? Spam { get; set; }

    public ContactFormSettings? ContactForm { get; set; }

    public FileStorageSettings? FileStorage { get; set; }
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
