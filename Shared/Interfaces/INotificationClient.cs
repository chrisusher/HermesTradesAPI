namespace Shared.Interfaces;

public interface INotificationClient
{
    Task<bool> SendTradeNotificationAsync(string subject, string message, CancellationToken cancellationToken = default);

    Task<bool> SendScreenerNotificationAsync(string symbol, string subject, string message);

    Task<bool> SendSummaryNotificationAsync(string screenerName, string subject, string message);
}
