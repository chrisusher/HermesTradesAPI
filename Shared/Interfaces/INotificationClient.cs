namespace Shared.Interfaces;

public interface INotificationClient
{
    Task<bool> SendScreenerNotificationAsync(string symbol, string subject, string message);

    Task<bool> SendSummaryNotificationAsync(string screenerName, string subject, string message);
}
