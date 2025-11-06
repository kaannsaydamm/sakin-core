namespace Sakin.Analytics.BaselineWorker.Services;

public interface IBaselineCalculatorService
{
    Task CalculateAndStoreBaselinesAsync(CancellationToken cancellationToken = default);
}
