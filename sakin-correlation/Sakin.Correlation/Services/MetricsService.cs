using Prometheus;

namespace Sakin.Correlation.Services;

public class MetricsService : IMetricsService
{
    private static readonly Counter EventsProcessed = Metrics.CreateCounter(
        "sakin_correlation_events_processed_total",
        "Total number of events processed by the correlation engine");

    private static readonly Counter RulesEvaluated = Metrics.CreateCounter(
        "sakin_correlation_rules_evaluated_total",
        "Total number of rules evaluated");

    private static readonly Counter AlertsCreated = Metrics.CreateCounter(
        "sakin_correlation_alerts_created_total",
        "Total number of alerts created");

    private static readonly Counter RedisOperations = Metrics.CreateCounter(
        "sakin_correlation_redis_ops_total",
        "Total number of Redis operations performed");

    private static readonly Histogram ProcessingLatency = Metrics.CreateHistogram(
        "sakin_correlation_processing_latency_ms",
        "Processing latency in milliseconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(1, 2, 10)
        });

    public void IncrementEventsProcessed()
    {
        EventsProcessed.Inc();
    }

    public void IncrementRulesEvaluated()
    {
        RulesEvaluated.Inc();
    }

    public void IncrementAlertsCreated()
    {
        AlertsCreated.Inc();
    }

    public void IncrementRedisOperations()
    {
        RedisOperations.Inc();
    }

    public void RecordProcessingLatency(double milliseconds)
    {
        ProcessingLatency.Observe(milliseconds);
    }
}
