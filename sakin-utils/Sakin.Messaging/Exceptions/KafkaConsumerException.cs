namespace Sakin.Messaging.Exceptions
{
    public class KafkaConsumerException : Exception
    {
        public string? Topic { get; }
        public int? Partition { get; }
        public long? Offset { get; }

        public KafkaConsumerException(string message) : base(message)
        {
        }

        public KafkaConsumerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public KafkaConsumerException(string message, string topic, int? partition = null, long? offset = null)
            : base(message)
        {
            Topic = topic;
            Partition = partition;
            Offset = offset;
        }

        public KafkaConsumerException(string message, string topic, int? partition, long? offset, Exception innerException)
            : base(message, innerException)
        {
            Topic = topic;
            Partition = partition;
            Offset = offset;
        }
    }
}
