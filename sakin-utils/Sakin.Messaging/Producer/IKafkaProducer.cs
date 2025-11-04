namespace Sakin.Messaging.Producer
{
    public interface IKafkaProducer : IDisposable
    {
        Task<MessageResult> ProduceAsync<T>(string topic, T message, string? key = null, CancellationToken cancellationToken = default);
        Task<MessageResult> ProduceAsync<T>(T message, string? key = null, CancellationToken cancellationToken = default);
        void Flush(TimeSpan timeout);
        Task FlushAsync(CancellationToken cancellationToken = default);
    }

    public record MessageResult
    {
        public string Topic { get; init; } = string.Empty;
        public int Partition { get; init; }
        public long Offset { get; init; }
        public DateTime Timestamp { get; init; }
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
