using System.Net.Http.Json;
using Shared.Interfaces;

namespace Services.Clients.Notifications;

public class SlackNotificationClient : INotificationClient
{
    private const string _screenerSlackUrlSecretName = "SLACK-SCREENER-WEBHOOKURL";
    private readonly ISecretClient _secretClient;
    private readonly CalendarClient _calendarClient;
    private readonly ILogger<SlackNotificationClient> _logger;

    public SlackNotificationClient(
        ISecretClient secretClient,
        CalendarClient calendarClient,
        ILoggerFactory loggerFactory)
    {
        _secretClient = secretClient;
        _calendarClient = calendarClient;
        _logger = loggerFactory.CreateLogger<SlackNotificationClient>();
    }

    public async Task<bool> SendScreenerNotificationAsync(string symbol, string subject, string message)
    {
        if (!await _calendarClient.ShouldSendNotificationsAsync())
        {
            return false;
        }

        var webhookUrl = await _secretClient.GetSecretAsync(_screenerSlackUrlSecretName);

        _logger.LogInformation("Sending Screener notification to Slack: '{symbol} - {subject}'...", symbol, subject);

        var result = await SendNotificationAsync(webhookUrl, subject, message);

        if (result)
        {
            _logger.LogInformation("Sent Screener notification to Slack: '{symbol} - {subject}' successfully!", symbol, subject);
        }

        return result;
    }

    public async Task<bool> SendSummaryNotificationAsync(string screenerName, string subject, string message)
    {
        if (!await _calendarClient.ShouldSendNotificationsAsync())
        {
            return false;
        }

        var webhookUrl = await _secretClient.GetSecretAsync(_screenerSlackUrlSecretName);

        _logger.LogInformation("Sending Summary notification to Slack: '{screenerName} - {subject}'...", screenerName, subject);

        var result = await SendNotificationAsync(webhookUrl, subject, message);

        if (result)
        {
            _logger.LogInformation("Sent Summary notification to Slack: '{screenerName} - {subject}' successfully!", screenerName, subject);
        }

        return result;
    }

    private async Task<bool> SendNotificationAsync(string webhookUrl, string subject, string message)
    {
        try
        {
            // Create the payload
            var payload = new
            {
                text = $"{subject}{Environment.NewLine}{message}",
            };

            var httpClient = new HttpClient();

            // Post the message to Slack
            var response = await httpClient.PostAsJsonAsync(webhookUrl, payload);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to send message to Slack. Status code: {response.StatusCode}, reason: {response.ReasonPhrase}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification");
            throw;
        }
    }
}
