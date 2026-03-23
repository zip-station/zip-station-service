using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using ZipStation.Business.Helpers;

namespace ZipStation.Business.Services;

public interface IConnectionTestService
{
    Task<(bool Success, string Message)> TestImapAsync(string host, int port, string username, string password, bool useSsl);
    Task<(bool Success, string Message)> TestSmtpAsync(string host, int port, string username, string password, bool useSsl);
}

public class ConnectionTestService : IConnectionTestService
{
    private readonly ILogger<ConnectionTestService> _logger;

    public ConnectionTestService(ILogger<ConnectionTestService> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> TestImapAsync(string host, int port, string username, string password, bool useSsl)
    {
        try
        {
            using var client = new ImapClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            client.Timeout = 10000;

            var options = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(host, port, options);
            await client.AuthenticateAsync(username, password);

            var inbox = client.Inbox;
            await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);
            var count = inbox.Count;

            await client.DisconnectAsync(true);

            return (true, $"Connected successfully. {count} messages in inbox.");
        }
        catch (AuthenticationException)
        {
            return (false, "Authentication failed. Check your username and password.");
        }
        catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("No such host"))
        {
            return (false, $"Could not connect to {host}:{port}. Check the host and port.");
        }
        catch (TimeoutException)
        {
            return (false, $"Connection timed out. Check the host, port, and SSL setting.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IMAP test failed for {Host}:{Port}", host, port);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Message)> TestSmtpAsync(string host, int port, string username, string password, bool useSsl)
    {
        try
        {
            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            client.Timeout = 10000;

            var options = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(host, port, options);
            await client.AuthenticateAsync(username, password);
            await client.DisconnectAsync(true);

            return (true, "Connected and authenticated successfully.");
        }
        catch (AuthenticationException)
        {
            return (false, "Authentication failed. Check your username and password.");
        }
        catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("No such host"))
        {
            return (false, $"Could not connect to {host}:{port}. Check the host and port.");
        }
        catch (TimeoutException)
        {
            return (false, $"Connection timed out. Check the host, port, and SSL setting.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP test failed for {Host}:{Port}", host, port);
            return (false, ex.Message);
        }
    }
}
