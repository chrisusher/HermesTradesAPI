using System.Text;
using Shared.Interfaces;

namespace Services.Repositories;

public class ReportRepository
{
    private readonly IStorageService _storageService;
    private readonly ILogger<ReportRepository> _logger;

    public ReportRepository(
        IStorageService storageService,
        ILoggerFactory loggerFactory)
    {
        _storageService = storageService;
        _storageService.FolderName = "reports";
        _logger = loggerFactory.CreateLogger<ReportRepository>();
    }

    public async Task<T?> GetLatestReportAsync<T>(string reportPrefix)
    {
        var file = await _storageService.ReadLatestFileAsync<T>(reportPrefix);

        if (file is null)
        {
            _logger.LogWarning("No report found with prefix {ReportPrefix}", reportPrefix);
            return default;
        }

        return file;
    }

    public async Task<T?> GetReportAsync<T>(string pathPrefix, DateTime reportDate)
    {
        var reportContent = await _storageService.ReadFileAsync<T>($"{pathPrefix}/{reportDate:yyyy-MM-dd}.json");

        return reportContent ?? default;
    }

    public async Task SaveReportAsync<T>(T reportData, string pathPrefix, DateTime reportDate)
    {
        await _storageService.SaveFileAsync(reportData, $"{pathPrefix}/{reportDate:yyyy-MM-dd}.json");
    }
}
