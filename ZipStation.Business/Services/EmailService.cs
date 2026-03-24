using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using ZipStation.Business.Helpers;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Services;

public interface IEmailService
{
    Task<(bool Success, string? Error)> SendReplyAsync(
        Project project,
        Ticket ticket,
        TicketMessage message,
        string toEmail,
        string? toName,
        TicketMessage? previousMessage = null);

    Task SendAutoReplyAsync(Project project, Ticket ticket, string toEmail, string? toName);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Success, string? Error)> SendReplyAsync(
        Project project,
        Ticket ticket,
        TicketMessage message,
        string toEmail,
        string? toName,
        TicketMessage? previousMessage = null)
    {
        var smtp = project.Settings?.Smtp;
        if (smtp == null || string.IsNullOrEmpty(smtp.Host))
        {
            return (false, "SMTP not configured for this project");
        }

        try
        {
            var fromEmail = smtp.FromEmail ?? smtp.Username;
            var fromName = smtp.FromName ?? project.Name;

            // Build the MIME message
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(fromName, fromEmail));
            mimeMessage.To.Add(new MailboxAddress(toName ?? toEmail, toEmail));
            mimeMessage.Subject = $"Re: {ticket.Subject}";

            // Generate a stable Message-ID for threading
            var domain = fromEmail.Contains('@') ? fromEmail.Split('@')[1] : "zipstation.local";
            var messageId = $"<{message.Id}@{domain}>";
            mimeMessage.MessageId = messageId;

            // Set threading headers — use ticket ID as the thread anchor
            var threadId = $"<ticket-{ticket.Id}@{domain}>";
            mimeMessage.InReplyTo = threadId;
            mimeMessage.References.Add(threadId);

            // Build body with optional signature
            var signature = project.Settings?.EmailSignature;
            var bodyHtml = message.BodyHtml ?? "";
            var bodyText = message.Body ?? "";

            if (signature != null && signature.Enabled && !string.IsNullOrEmpty(signature.SignatureHtml))
            {
                bodyHtml = $"{bodyHtml}<br/><br/>--<br/>{signature.SignatureHtml}";
                bodyText = $"{bodyText}\n\n--\n{signature.SignatureHtml.Replace("<br/>", "\n").Replace("<br>", "\n")}";
            }

            // Append quoted previous message for context
            if (previousMessage != null)
            {
                var quotedAuthor = previousMessage.AuthorName ?? previousMessage.AuthorEmail ?? toEmail;
                var quotedDate = DateTimeOffset.FromUnixTimeMilliseconds(previousMessage.CreatedOnDateTime)
                    .ToString("ddd, MMM d, yyyy 'at' h:mm tt");
                var quoteHeader = $"On {quotedDate}, {quotedAuthor} wrote:";

                if (!string.IsNullOrEmpty(previousMessage.BodyHtml))
                {
                    bodyHtml += $"<br/><br/><div class=\"gmail_quote\">{quoteHeader}<br/><blockquote style=\"margin:0 0 0 .8ex;border-left:1px solid #ccc;padding-left:1ex\">{previousMessage.BodyHtml}</blockquote></div>";
                }
                else if (!string.IsNullOrEmpty(previousMessage.Body))
                {
                    var quotedLines = previousMessage.Body.Split('\n').Select(l => $"&gt; {System.Net.WebUtility.HtmlEncode(l)}");
                    bodyHtml += $"<br/><br/><div class=\"gmail_quote\">{quoteHeader}<br/><blockquote style=\"margin:0 0 0 .8ex;border-left:1px solid #ccc;padding-left:1ex\"><pre>{string.Join("\n", quotedLines)}</pre></blockquote></div>";
                }

                if (!string.IsNullOrEmpty(previousMessage.Body))
                {
                    var quotedText = string.Join("\n", previousMessage.Body.Split('\n').Select(l => $"> {l}"));
                    bodyText += $"\n\n{quoteHeader}\n{quotedText}";
                }
            }

            var bodyBuilder = new BodyBuilder();
            if (!string.IsNullOrEmpty(bodyHtml))
            {
                bodyBuilder.HtmlBody = bodyHtml;
                bodyBuilder.TextBody = bodyText;
            }
            else
            {
                bodyBuilder.TextBody = bodyText;
            }
            mimeMessage.Body = bodyBuilder.ToMessageBody();

            // Send
            var password = EncryptionHelper.Decrypt(smtp.Password);
            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            var secureSocketOptions = smtp.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(smtp.Host, smtp.Port, secureSocketOptions);
            await client.AuthenticateAsync(smtp.Username, password);
            await client.SendAsync(mimeMessage);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {ToEmail} for ticket {TicketId} via {SmtpHost}",
                toEmail, ticket.Id, smtp.Host);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} for ticket {TicketId}", toEmail, ticket.Id);
            return (false, ex.Message);
        }
    }

    public async Task SendAutoReplyAsync(Project project, Ticket ticket, string toEmail, string? toName)
    {
        var autoReply = project.Settings?.AutoReply;
        if (autoReply == null || !autoReply.Enabled) return;

        var smtp = project.Settings?.Smtp;
        if (smtp == null || string.IsNullOrEmpty(smtp.Host)) return;

        try
        {
            var fromEmail = smtp.FromEmail ?? smtp.Username;
            var fromName = smtp.FromName ?? project.Name;

            var ticketIdSettings = project.Settings?.TicketId ?? new TicketIdSettings();
            var minLen = Math.Max(ticketIdSettings.MinLength, 3);
            var displayId = ticket.TicketNumber.ToString().PadLeft(minLen, '0');
            if (!string.IsNullOrEmpty(ticketIdSettings.Prefix))
                displayId = $"{ticketIdSettings.Prefix}-{displayId}";

            var subject = autoReply.SubjectTemplate
                .Replace("{TicketSubject}", ticket.Subject)
                .Replace("{TicketId}", displayId)
                .Replace("{ProjectName}", project.Name);

            var body = autoReply.BodyTemplate
                .Replace("{CustomerName}", toName ?? toEmail.Split('@')[0])
                .Replace("{CustomerEmail}", toEmail)
                .Replace("{TicketId}", displayId)
                .Replace("{TicketSubject}", ticket.Subject)
                .Replace("{ProjectName}", project.Name);

            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(fromName, fromEmail));
            mimeMessage.To.Add(new MailboxAddress(toName ?? toEmail, toEmail));
            mimeMessage.Subject = subject;

            var domain = fromEmail.Contains('@') ? fromEmail.Split('@')[1] : "zipstation.local";
            mimeMessage.MessageId = $"<autoreply-{ticket.Id}@{domain}>";
            mimeMessage.InReplyTo = $"<ticket-{ticket.Id}@{domain}>";

            // Append signature if enabled
            var signature = project.Settings?.EmailSignature;
            if (signature != null && signature.Enabled && !string.IsNullOrEmpty(signature.SignatureHtml))
                body = $"{body}<br/><br/>--<br/>{signature.SignatureHtml}";

            var bodyBuilder = new BodyBuilder { HtmlBody = body };
            mimeMessage.Body = bodyBuilder.ToMessageBody();

            var password = EncryptionHelper.Decrypt(smtp.Password);
            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            var secureSocketOptions = smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(smtp.Host, smtp.Port, secureSocketOptions);
            await client.AuthenticateAsync(smtp.Username, password);
            await client.SendAsync(mimeMessage);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Auto-reply sent to {ToEmail} for ticket {TicketId}", toEmail, ticket.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send auto-reply to {ToEmail} for ticket {TicketId}", toEmail, ticket.Id);
        }
    }
}
