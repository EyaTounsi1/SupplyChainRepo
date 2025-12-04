using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PartTracker;

public class ExcelSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ExcelSyncBackgroundService> _logger;

    public ExcelSyncBackgroundService(
        IServiceProvider services,
        ILogger<ExcelSyncBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1)); // adjust as needed

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _services.CreateScope();
                var importer = scope.ServiceProvider.GetRequiredService<IExcelImportService>();
                await importer.ImportChangeLogAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Excel sync.");
            }
        }
    }
}