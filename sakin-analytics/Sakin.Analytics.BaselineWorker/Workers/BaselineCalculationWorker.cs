using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Analytics.BaselineWorker.Services;

namespace Sakin.Analytics.BaselineWorker.Workers;

public class BaselineCalculationWorker : BackgroundService
{
    private readonly ILogger<BaselineCalculationWorker> _logger;
    private readonly IBaselineCalculatorService _calculatorService;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public BaselineCalculationWorker(
        ILogger<BaselineCalculationWorker> logger,
        IBaselineCalculatorService calculatorService)
    {
        _logger = logger;
        _calculatorService = calculatorService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BaselineCalculationWorker starting with interval: {Interval}", _interval);

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        using var timer = new PeriodicTimer(_interval);

        try
        {
            do
            {
                _logger.LogInformation("Starting baseline calculation cycle...");
                
                try
                {
                    await _calculatorService.CalculateAndStoreBaselinesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during baseline calculation cycle");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BaselineCalculationWorker is stopping");
        }
    }
}
