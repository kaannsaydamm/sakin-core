namespace Sakin.HttpCollector.Services;

public interface IMetricsService
{
    void IncrementHttpRequests(string sourceIp, string format, int statusCode);
    void RecordHttpRequestDuration(double seconds);
    void IncrementHttpErrors(int errorCode);
    void IncrementKafkaMessagesPublished(string topic);
}
