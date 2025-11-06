namespace Sakin.Correlation.Services;

public interface IMetricsService
{
    void IncrementEventsProcessed();
    void IncrementRulesEvaluated();
    void IncrementAlertsCreated();
    void IncrementRedisOperations();
    void RecordProcessingLatency(double milliseconds);
}
