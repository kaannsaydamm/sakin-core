using Prometheus;

namespace Sakin.HttpCollector.Services;

public class MetricsService : IMetricsService
{
    private static readonly Counter HttpRequestsTotal = Metrics.CreateCounter(
        "sakin_http_requests_total",
        "Total number of HTTP requests received",
        new CounterConfiguration
        {
            LabelNames = new[] { "source_ip", "format", "status_code" }
        });

    private static readonly Histogram HttpRequestDuration = Metrics.CreateHistogram(
        "sakin_http_request_duration_seconds",
        "HTTP request duration in seconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
        });

    private static readonly Counter HttpErrorsTotal = Metrics.CreateCounter(
        "sakin_http_errors_total",
        "Total number of HTTP errors",
        new CounterConfiguration
        {
            LabelNames = new[] { "error_code" }
        });

    private static readonly Counter KafkaMessagesPublishedTotal = Metrics.CreateCounter(
        "sakin_kafka_messages_published_total",
        "Total number of messages published to Kafka",
        new CounterConfiguration
        {
            LabelNames = new[] { "topic" }
        });

    public void IncrementHttpRequests(string sourceIp, string format, int statusCode)
    {
        HttpRequestsTotal.WithLabels(sourceIp, format, statusCode.ToString()).Inc();
    }

    public void RecordHttpRequestDuration(double seconds)
    {
        HttpRequestDuration.Observe(seconds);
    }

    public void IncrementHttpErrors(int errorCode)
    {
        HttpErrorsTotal.WithLabels(errorCode.ToString()).Inc();
    }

    public void IncrementKafkaMessagesPublished(string topic)
    {
        KafkaMessagesPublishedTotal.WithLabels(topic).Inc();
    }
}
