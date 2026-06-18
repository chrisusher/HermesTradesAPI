using Microsoft.Extensions.DependencyInjection;
using Services.Clients;
using Services.Reports;
using Services.Repositories;
using Shared.DTOs.Reports;
using Shared.DTOs.Reports.Portfolio;
using Shared.DTOs.Reports.Strategy;

namespace Services;

public class ReportService
{
    private readonly ReportRepository _reportRepository;
    private readonly ServiceProvider _serviceProvider;
    private readonly StrategyService _strategyService;
    private readonly PortfolioService _portfolioService;
    private readonly StockClient _stockClient;
    private readonly TransactionService _transactionService;
    private readonly FxRateClient _fxRateClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        ReportRepository reportRepository,
        ServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
    {
        _reportRepository = reportRepository;
        _serviceProvider = serviceProvider;
        _strategyService = _serviceProvider.GetRequiredService<StrategyService>();
        _portfolioService = _serviceProvider.GetRequiredService<PortfolioService>();
        _stockClient = _serviceProvider.GetRequiredService<StockClient>();
        _strategyService = _serviceProvider.GetRequiredService<StrategyService>();
        _transactionService = _serviceProvider.GetRequiredService<TransactionService>();
        _fxRateClient = _serviceProvider.GetRequiredService<FxRateClient>();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ReportService>();
    }

    public async Task<StrategyReport> CreateReportAsync(ReportType reportType, Dictionary<string, object> parameters)
    {
        switch (reportType)
        {
            case ReportType.StrategyPerformance:
                var strategyReporter = new StrategyPerformanceReporter(_reportRepository, _strategyService);
                return await strategyReporter.CreateReportAsync(parameters);
            case ReportType.SinglePortfolioROI:
                var portfolioReporter = new PortfolioROIReporter(_portfolioService, _stockClient, _strategyService, _transactionService, _fxRateClient, _loggerFactory);
                return await portfolioReporter.CreateReportAsync(parameters);
            case ReportType.AllPortfolioROIs:
                var allPortfolioReporter = new PortfolioROIReporter(_portfolioService, _stockClient, _strategyService, _transactionService, _fxRateClient, _loggerFactory);
                return await allPortfolioReporter.CreateReportAsync(parameters);
            default:
                throw new NotImplementedException($"Report type {reportType} not implemented");
        }
    }

    public async Task<StrategyReport> GetReportAsync(ReportType reportType, string entityId, DateTime reportDate)
    {
        return reportType switch
        {
            ReportType.StrategyPerformance => await GetReportAsync<StrategyPerformanceReport>(reportType, entityId, reportDate),
            ReportType.SinglePortfolioROI => await GetReportAsync<PortfolioROIReport>(reportType, entityId, reportDate),
            ReportType.AllPortfolioROIs => await GetReportAsync<AllPortfolioROIsReport>(reportType, entityId, reportDate),
            _ => throw new NotImplementedException($"Report type {reportType} not implemented")
        };
    }

    private async Task<T> GetReportAsync<T>(ReportType reportType, string entityId, DateTime reportDate) where T : StrategyReport
    {
        var existingReport = await _reportRepository.GetReportAsync<T>($"{reportType}/{entityId}", reportDate);

        if (existingReport is not null)
        {
            return existingReport;
        }

        throw new DataNotFoundException($"No report found for object {entityId} of type {typeof(T).Name}");
    }
}
