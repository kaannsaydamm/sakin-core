using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Exceptions;
using Sakin.Messaging.Serialization;

namespace Sakin.Messaging.Consumer
{
    public class KafkaConsumer : IKafkaConsumer
    {
        private readonly IConsumer<string, byte[]> _consumer;
        private readonly ConsumerOptions _consumerOptions;
        private readonly IMessageSerializer _serializer;
        private readonly ILogger<KafkaConsumer> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private bool _disposed;

        public KafkaConsumer(
            IOptions<KafkaOptions> kafkaOptions,
            IOptions<ConsumerOptions> consumerOptions,
            IMessageSerializer serializer,
            ILogger<KafkaConsumer> logger)
        {
            _consumerOptions = consumerOptions.Value;
            _serializer = serializer;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = kafkaOptions.Value.BootstrapServers,
                GroupId = _consumerOptions.GroupId,
                ClientId = kafkaOptions.Value.ClientId,
                AutoOffsetReset = MapAutoOffsetReset(_consumerOptions.AutoOffsetReset),
                EnableAutoCommit = _consumerOptions.EnableAutoCommit,
                AutoCommitIntervalMs = _consumerOptions.AutoCommitIntervalMs,
                SessionTimeoutMs = _consumerOptions.SessionTimeoutMs,
                MaxPollIntervalMs = _consumerOptions.MaxPollIntervalMs,
                FetchMinBytes = _consumerOptions.FetchMinBytes,
                SecurityProtocol = MapSecurityProtocol(kafkaOptions.Value.SecurityProtocol)
            };

            if (!string.IsNullOrWhiteSpace(kafkaOptions.Value.SaslMechanism))
            {
                config.SaslMechanism = Enum.Parse<SaslMechanism>(kafkaOptions.Value.SaslMechanism, true);
                config.SaslUsername = kafkaOptions.Value.SaslUsername;
                config.SaslPassword = kafkaOptions.Value.SaslPassword;
            }

            _consumer = new ConsumerBuilder<string, byte[]>(config)
                .SetErrorHandler((_, error) =>
                {
                    _logger.LogError("Kafka consumer error: Code={Code}, Reason={Reason}, IsFatal={IsFatal}",
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
                    _logger.Log(logLevel, "Kafka consumer log: {Message}", message.Message);
                })
                .Build();

            if (_consumerOptions.Topics.Length > 0)
            {
                _consumer.Subscribe(_consumerOptions.Topics);
                _logger.LogInformation("Kafka consumer subscribed to topics: {Topics}",
                    string.Join(", ", _consumerOptions.Topics));
            }

            _retryPolicy = Policy
                .Handle<Exception>(ex => !(ex is OperationCanceledException))
                .WaitAndRetryAsync(
                    _consumerOptions.MaxRetries,
                    retryAttempt => TimeSpan.FromMilliseconds(_consumerOptions.RetryDelayMs * retryAttempt),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception,
                            "Retry {RetryCount}/{MaxRetries} after {Delay}ms due to: {Message}",
                            retryCount, _consumerOptions.MaxRetries, timespan.TotalMilliseconds, exception.Message);
                    });

            _logger.LogInformation("Kafka consumer initialized with group ID: {GroupId}, bootstrap servers: {BootstrapServers}",
                _consumerOptions.GroupId, kafkaOptions.Value.BootstrapServers);
        }

        public async Task ConsumeAsync<T>(Func<ConsumeResult<T>, Task> messageHandler, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KafkaConsumer));
            }

            if (messageHandler == null)
            {
                throw new ArgumentNullException(nameof(messageHandler));
            }

            _logger.LogInformation("Starting message consumption");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(cancellationToken);

                    if (consumeResult?.Message == null)
                    {
                        continue;
                    }

                    _logger.LogDebug("Consumed message from topic {Topic}, partition {Partition}, offset {Offset}",
                        consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value);

                    await _retryPolicy.ExecuteAsync(async () =>
                    {
                        try
                        {
                            var deserializedMessage = _serializer.Deserialize<T>(consumeResult.Message.Value);

                            var result = new ConsumeResult<T>
                            {
                                Topic = consumeResult.Topic,
                                Partition = consumeResult.Partition.Value,
                                Offset = consumeResult.Offset.Value,
                                Key = consumeResult.Message.Key,
                                Message = deserializedMessage,
                                Timestamp = consumeResult.Message.Timestamp.UtcDateTime
                            };

                            await messageHandler(result);

                            if (!_consumerOptions.EnableAutoCommit)
                            {
                                _consumer.Commit(consumeResult);
                                _logger.LogDebug("Committed offset {Offset} for topic {Topic}, partition {Partition}",
                                    consumeResult.Offset.Value, consumeResult.Topic, consumeResult.Partition.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Error processing message from topic {Topic}, partition {Partition}, offset {Offset}",
                                consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value);
                            throw;
                        }
                    });
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message: {Error}", ex.Error.Reason);
                    throw new KafkaConsumerException($"Error consuming message: {ex.Error.Reason}", ex);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Message consumption cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during message consumption after retries, continuing...");
                }
            }

            _logger.LogInformation("Message consumption stopped");
        }

        public void Commit()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KafkaConsumer));
            }

            try
            {
                _consumer.Commit();
                _logger.LogDebug("Manual commit completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error committing offsets");
                throw new KafkaConsumerException("Error committing offsets", ex);
            }
        }

        public void Subscribe(params string[] topics)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KafkaConsumer));
            }

            if (topics == null || topics.Length == 0)
            {
                throw new ArgumentException("At least one topic must be specified", nameof(topics));
            }

            try
            {
                _consumer.Subscribe(topics);
                _logger.LogInformation("Subscribed to topics: {Topics}", string.Join(", ", topics));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to topics");
                throw new KafkaConsumerException("Error subscribing to topics", ex);
            }
        }

        public void Unsubscribe()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KafkaConsumer));
            }

            try
            {
                _consumer.Unsubscribe();
                _logger.LogInformation("Unsubscribed from all topics");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from topics");
                throw new KafkaConsumerException("Error unsubscribing from topics", ex);
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
                _logger.LogInformation("Disposing Kafka consumer");
                _consumer.Close();
                _consumer.Dispose();
                _disposed = true;
                _logger.LogInformation("Kafka consumer disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Kafka consumer");
            }
        }

        private static Confluent.Kafka.AutoOffsetReset MapAutoOffsetReset(Configuration.AutoOffsetReset autoOffsetReset) => autoOffsetReset switch
        {
            Configuration.AutoOffsetReset.Earliest => Confluent.Kafka.AutoOffsetReset.Earliest,
            Configuration.AutoOffsetReset.Latest => Confluent.Kafka.AutoOffsetReset.Latest,
            Configuration.AutoOffsetReset.Error => Confluent.Kafka.AutoOffsetReset.Error,
            _ => Confluent.Kafka.AutoOffsetReset.Earliest
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
