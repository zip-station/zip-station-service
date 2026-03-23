using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZipStation.Business.Repositories;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Services;

public interface IAlertService
{
    Task FireAlertsAsync(string companyId, string projectId, AlertTriggerType triggerType, string? triggerValue, Dictionary<string, string> context);
}

public class AlertService : IAlertService
{
    private readonly IAlertRepository _alertRepository;
    private static readonly HttpClient _httpClient = new();
    private readonly ILogger<AlertService> _logger;

    public AlertService(IAlertRepository alertRepository, ILogger<AlertService> logger)
    {
        _alertRepository = alertRepository;
        _logger = logger;
    }

    public async Task FireAlertsAsync(string companyId, string projectId, AlertTriggerType triggerType, string? triggerValue, Dictionary<string, string> context)
    {
        try
        {
            var alerts = await _alertRepository.GetEnabledByProjectIdAsync(projectId);
            var matchingAlerts = alerts.Where(a => a.TriggerType == triggerType).ToList();

            // For keyword triggers, filter by whether the triggerValue contains the keyword
            if (triggerType == AlertTriggerType.KeywordInSubject || triggerType == AlertTriggerType.KeywordInBody)
            {
                matchingAlerts = matchingAlerts
                    .Where(a => !string.IsNullOrEmpty(a.TriggerValue)
                        && !string.IsNullOrEmpty(triggerValue)
                        && triggerValue.Contains(a.TriggerValue, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // For CustomerContact triggers, match on customer email
            if (triggerType == AlertTriggerType.CustomerContact)
            {
                matchingAlerts = matchingAlerts
                    .Where(a => !string.IsNullOrEmpty(a.TriggerValue)
                        && !string.IsNullOrEmpty(triggerValue)
                        && triggerValue.Equals(a.TriggerValue, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (matchingAlerts.Count == 0)
            {
                _logger.LogDebug("No alerts matched trigger {TriggerType} for project {ProjectId}", triggerType, projectId);
            }

            foreach (var alert in matchingAlerts)
            {
                try
                {
                    _logger.LogDebug("Sending webhook to {WebhookHost} for alert {AlertName}", new Uri(alert.WebhookUrl).Host, alert.Name);
                    await SendWebhookAsync(alert, context);
                    _logger.LogInformation("Alert fired successfully: {AlertId} ({AlertName}) for project {ProjectId}", alert.Id, alert.Name, projectId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fire alert {AlertId} ({AlertName}) for project {ProjectId}", alert.Id, alert.Name, projectId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error firing alerts for project {ProjectId}, trigger {TriggerType}", projectId, triggerType);
        }
    }

    private async Task SendWebhookAsync(Alert alert, Dictionary<string, string> context)
    {
        var client = _httpClient;
        client.Timeout = TimeSpan.FromSeconds(10);

        string payload;

        if (!string.IsNullOrEmpty(alert.CustomPayloadTemplate))
        {
            payload = alert.CustomPayloadTemplate;
            foreach (var kvp in context)
            {
                payload = payload.Replace($"{{{kvp.Key}}}", kvp.Value);
            }
        }
        else
        {
            payload = BuildDefaultPayload(alert, context);
        }

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(alert.WebhookUrl, content);
        _logger.LogDebug("Webhook response {StatusCode} for alert {AlertName}", response.StatusCode, alert.Name);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Webhook returned {StatusCode} for alert {AlertName} ({WebhookUrl}): {ResponseBody}",
                response.StatusCode, alert.Name, new Uri(alert.WebhookUrl).Host, responseBody.Length > 500 ? responseBody[..500] : responseBody);
        }
    }

    private static string BuildDefaultPayload(Alert alert, Dictionary<string, string> context)
    {
        context.TryGetValue("projectName", out var projectName);
        context.TryGetValue("ticketId", out var ticketId);
        context.TryGetValue("subject", out var subject);
        context.TryGetValue("customerEmail", out var customerEmail);

        var message = $"[{projectName ?? "Unknown Project"}] New ticket from {customerEmail ?? "unknown"}: {subject ?? "No subject"}";

        switch (alert.ChannelType)
        {
            case AlertChannelType.Slack:
                return JsonSerializer.Serialize(new { text = message });

            case AlertChannelType.Discord:
                return JsonSerializer.Serialize(new { content = message });

            case AlertChannelType.GenericWebhook:
            default:
                return JsonSerializer.Serialize(new
                {
                    @event = alert.TriggerType.ToString(),
                    ticketId = ticketId ?? "",
                    subject = subject ?? "",
                    customerEmail = customerEmail ?? "",
                    projectName = projectName ?? ""
                });
        }
    }
}
