namespace Sakin.Messaging.Consumer
{
    public interface IKafkaConsumer : IDisposable
    {
        Task ConsumeAsync<T>(Func<ConsumeResult<T>, Task> messageHandler, CancellationToken cancellationToken);
        void Commit();
        void Subscribe(params string[] topics);
        void Unsubscribe();
    }

    public record ConsumeResult<T>
    {
        public string Topic { get; init; } = string.Empty;
        public int Partition { get; init; }
        public long Offset { get; init; }
        public string? Key { get; init; }
        public T? Message { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
