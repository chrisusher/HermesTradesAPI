using Services.Repositories;
using Shared.DTOs.Reports;
using Shared.DTOs.Reports.Strategy;
using Shared.Interfaces;

namespace Services.Reports;

public class StrategyPerformanceReporter : IStrategyReporter
{
    private readonly ReportRepository _reportRepository;
    private readonly StrategyService _strategyService;

    public StrategyPerformanceReporter(
        ReportRepository reportRepository,
        StrategyService strategyService)
    {
        _reportRepository = reportRepository;
        _strategyService = strategyService;
    }

    public async Task<StrategyReport> CreateReportAsync(Dictionary<string, object> parameters, DateTime? now = null)
    {
        if (!parameters.TryGetValue("strategyId", out object? value))
        {
            throw new ArgumentException("Parameter 'strategyId' is required");
        }

        var strategyId = value.ToString()!;

        var strategy = await _strategyService.GetStrategyAsync(strategyId);

        now ??= DateTime.UtcNow;

        var existingReport = await _reportRepository.GetReportAsync<StrategyPerformanceReport>($"{ReportType.StrategyPerformance}/{strategyId}", now.Value);

        if (existingReport is not null)
        {
            return existingReport;
        }

        var report = new StrategyPerformanceReport
        {
            StrategyName = strategy.Name,
        };

        await _reportRepository.SaveReportAsync(report, $"{ReportType.StrategyPerformance}/{strategyId}", now.Value);

        return report;
    }
}
