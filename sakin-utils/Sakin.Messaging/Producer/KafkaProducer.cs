using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Exceptions;
using Sakin.Messaging.Serialization;

namespace Sakin.Messaging.Producer
{
    public class KafkaProducer : IKafkaProducer
    {
        private readonly IProducer<string, byte[]> _producer;
        private readonly ProducerOptions _producerOptions;
        private readonly IMessageSerializer _serializer;
        private readonly ILogger<KafkaProducer> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private bool _disposed;

        public KafkaProducer(
            IOptions<KafkaOptions> kafkaOptions,
            IOptions<ProducerOptions> producerOptions,
            IMessageSerializer serializer,
            ILogger<KafkaProducer> logger)
        {
            _producerOptions = producerOptions.Value;
            _serializer = serializer;
            _logger = logger;

            var config = new ProducerConfig
            {
                BootstrapServers = kafkaOptions.Value.BootstrapServers,
                ClientId = kafkaOptions.Value.ClientId,
                MessageTimeoutMs = kafkaOptions.Value.MessageTimeoutMs,
                RequestTimeoutMs = kafkaOptions.Value.RequestTimeoutMs,
                Acks = MapAcks(_producerOptions.RequiredAcks),
                EnableIdempotence = _producerOptions.EnableIdempotence,
                MaxInFlight = _producerOptions.MaxInFlight,
                CompressionType = MapCompressionType(_producerOptions.CompressionType),
                BatchSize = _producerOptions.BatchSize,
                LingerMs = _producerOptions.LingerMs,
                RetryBackoffMs = _producerOptions.RetryBackoffMs,
                SecurityProtocol = MapSecurityProtocol(kafkaOptions.Value.SecurityProtocol)
            };

            if (!string.IsNullOrWhiteSpace(kafkaOptions.Value.SaslMechanism))
            {
                config.SaslMechanism = Enum.Parse<SaslMechanism>(kafkaOptions.Value.SaslMechanism, true);
                config.SaslUsername = kafkaOptions.Value.SaslUsername;
                config.SaslPassword = kafkaOptions.Value.SaslPassword;
            }

            _producer = new ProducerBuilder<string, byte[]>(config)
                .SetErrorHandler((_, error) =>
                {
                    _logger.LogError("Kafka producer error: Code={Code}, Reason={Reason}, IsFatal={IsFatal}",
                        error.Code, error.Reason, error.IsFatal);
                })
                .SetLogHandler((_, message) =>
                {
                    var logLevel = message.Level switch
                    {
                        SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical or SyslogLevel.Error => LogLevel.Error,
                        SyslogLevel.Warning => LogLevel.Warning,
                        SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
                        _ => LogLevel.Debug
                    };
                    _logger.Log(logLevel, "Kafka producer log: {Message}", message.Message);
                })
                .Build();

            _retryPolicy = Policy
                .Handle<ProduceException<string, byte[]>>()
                .WaitAndRetryAsync(
                    _producerOptions.RetryCount,
                    retryAttempt => TimeSpan.FromMilliseconds(_producerOptions.RetryBackoffMs * Math.Pow(2, retryAttempt - 1)),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception,
                            "Retry {RetryCount}/{MaxRetries} after {Delay}ms due to: {Message}",
                            retryCount, _producerOptions.RetryCount, timespan.TotalMilliseconds, exception.Message);
                    });

            _logger.LogInformation("Kafka producer initialized with bootstrap servers: {BootstrapServers}",
                kafkaOptions.Value.BootstrapServers);
        }

        public async Task<MessageResult> ProduceAsync<T>(string topic, T message, string? key = null, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KafkaProducer));
            }

            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new ArgumentException("Topic cannot be null or empty", nameof(topic));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            try
            {
                var serializedValue = _serializer.Serialize(message);
                var kafkaMessage = new Message<string, byte[]>
                {
                    Key = key ?? string.Empty,
                    Value = serializedValue,
                    Timestamp = Timestamp.Default
                };

                _logger.LogDebug("Producing message to topic {Topic} with key {Key}", topic, key ?? "null");

                var deliveryResult = await _retryPolicy.ExecuteAsync(async () =>
                    await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken));

                _logger.LogInformation(
                    "Message delivered to topic {Topic}, partition {Partition}, offset {Offset}",
                    deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);

                return new MessageResult
                {
                    Topic = deliveryResult.Topic,
                    Partition = deliveryResult.Partition.Value,
                    Offset = deliveryResult.Offset.Value,
                    Timestamp = deliveryResult.Message.Timestamp.UtcDateTime,
                    IsSuccess = true
                };
            }
            catch (ProduceException<string, byte[]> ex)
            {
                _logger.LogError(ex,
                    "Failed to produce message to topic {Topic} after {RetryCount} retries: {Error}",
                    topic, _producerOptions.RetryCount, ex.Error.Reason);

                throw new KafkaProducerException(
                    $"Failed to produce message to topic {topic}: {ex.Error.Reason}",
                    topic, key, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error producing message to topic {Topic}", topic);
                throw new KafkaProducerException($"Unexpected error producing message to topic {topic}", topic, key, ex);
            }
        }

        public Task<MessageResult> ProduceAsync<T>(T message, string? key = null, CancellationToken cancellationToken = default)
        {
            return ProduceAsync(_producerOptions.DefaultTopic, message, key, cancellationToken);
        }

        public void Flush(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KafkaProducer));
            }

            try
            {
                _logger.LogDebug("Flushing producer with timeout {Timeout}ms", timeout.TotalMilliseconds);
                _producer.Flush(timeout);
                _logger.LogDebug("Producer flush completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing producer");
                throw new KafkaProducerException("Error flushing producer", ex);
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KafkaProducer));
            }

            try
            {
                _logger.LogDebug("Flushing producer asynchronously");
                await Task.Run(() => _producer.Flush(cancellationToken), cancellationToken);
                _logger.LogDebug("Producer flush completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing producer");
                throw new KafkaProducerException("Error flushing producer", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Disposing Kafka producer");
                _producer.Flush(TimeSpan.FromSeconds(10));
                _producer.Dispose();
                _disposed = true;
                _logger.LogInformation("Kafka producer disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Kafka producer");
            }
        }

        private static Confluent.Kafka.Acks MapAcks(Configuration.Acks acks) => acks switch
        {
            Configuration.Acks.None => Confluent.Kafka.Acks.None,
            Configuration.Acks.Leader => Confluent.Kafka.Acks.Leader,
            Configuration.Acks.All => Confluent.Kafka.Acks.All,
            _ => Confluent.Kafka.Acks.Leader
        };

        private static Confluent.Kafka.CompressionType MapCompressionType(Configuration.CompressionType compressionType) => compressionType switch
        {
            Configuration.CompressionType.None => Confluent.Kafka.CompressionType.None,
            Configuration.CompressionType.Gzip => Confluent.Kafka.CompressionType.Gzip,
            Configuration.CompressionType.Snappy => Confluent.Kafka.CompressionType.Snappy,
            Configuration.CompressionType.Lz4 => Confluent.Kafka.CompressionType.Lz4,
            Configuration.CompressionType.Zstd => Confluent.Kafka.CompressionType.Zstd,
            _ => Confluent.Kafka.CompressionType.Snappy
        };

        private static Confluent.Kafka.SecurityProtocol MapSecurityProtocol(Configuration.SecurityProtocol securityProtocol) => securityProtocol switch
        {
            Configuration.SecurityProtocol.Plaintext => Confluent.Kafka.SecurityProtocol.Plaintext,
            Configuration.SecurityProtocol.Ssl => Confluent.Kafka.SecurityProtocol.Ssl,
            Configuration.SecurityProtocol.SaslPlaintext => Confluent.Kafka.SecurityProtocol.SaslPlaintext,
            Configuration.SecurityProtocol.SaslSsl => Confluent.Kafka.SecurityProtocol.SaslSsl,
            _ => Confluent.Kafka.SecurityProtocol.Plaintext
        };
    }
}
