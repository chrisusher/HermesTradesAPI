using System.Net.Http.Json;
using Shared.Interfaces;

namespace Services.Clients.Notifications;

public class SlackNotificationClient : INotificationClient, IDisposable
{
    private const string _screenerSlackUrlSecretName = "SLACK-SCREENER-WEBHOOKURL";
    private readonly ISecretClient _secretClient;
    private readonly CalendarClient _calendarClient;
    private readonly ILogger<SlackNotificationClient> _logger;
    private readonly HttpClient _httpClient;

    public SlackNotificationClient(
        ISecretClient secretClient,
        CalendarClient calendarClient,
        ILoggerFactory loggerFactory,
        HttpClient? httpClient = null)
    {
        _secretClient = secretClient;
        _calendarClient = calendarClient;
        _logger = loggerFactory.CreateLogger<SlackNotificationClient>();
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
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

    public async Task<bool> SendTradeNotificationAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        var webhookUrl = await _secretClient.GetSecretAsync(_screenerSlackUrlSecretName);

        return await SendNotificationAsync(webhookUrl, subject, message, cancellationToken);
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

    public void Dispose() => _httpClient.Dispose();

    private async Task<bool> SendNotificationAsync(
        string webhookUrl,
        string subject,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create the payload
            var payload = new
            {
                text = $"{subject}{Environment.NewLine}{message}",
            };

            // Post the message to Slack
            using var response = await _httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);

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
