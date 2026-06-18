using Shared.DTOs.Reports;

namespace Shared.Interfaces;

public interface IStrategyReporter
{
    Task<StrategyReport> CreateReportAsync(Dictionary<string, object> parameters, DateTime? now = null);
}
