namespace Sakin.Messaging.Exceptions
{
    public class KafkaProducerException : Exception
    {
        public string? Topic { get; }
        public string? Key { get; }

        public KafkaProducerException(string message) : base(message)
        {
        }

        public KafkaProducerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public KafkaProducerException(string message, string topic, string? key = null)
            : base(message)
        {
            Topic = topic;
            Key = key;
        }

        public KafkaProducerException(string message, string topic, string? key, Exception innerException)
            : base(message, innerException)
        {
            Topic = topic;
            Key = key;
        }
    }
}
