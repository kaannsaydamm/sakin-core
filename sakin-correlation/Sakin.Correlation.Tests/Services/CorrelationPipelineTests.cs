using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Correlation.Services;
using Sakin.Messaging.Consumer;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class CorrelationPipelineTests
{
    [Fact]
    public async Task EnqueueAsync_ProcessesEventsAndPublishesAlerts()
    {
        var ruleEngineMock = new Mock<IRuleEngine>();
        var alertRepositoryMock = new Mock<IAlertRepository>();
        var alertPublisherMock = new Mock<IAlertPublisher>();

        var options = Options.Create(new CorrelationPipelineOptions
        {
            MaxDegreeOfParallelism = 1,
            ChannelCapacity = 8,
            BatchSize = 2,
            BatchIntervalMilliseconds = 200
        });

        var pipeline = new CorrelationPipeline(
            ruleEngineMock.Object,
            alertRepositoryMock.Object,
            alertPublisherMock.Object,
            options,
            NullLogger<CorrelationPipeline>.Instance);

        var normalizedEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            SourceIp = "192.168.1.10",
            EventType = EventType.NetworkTraffic
        };

        var alerts = new List<Alert>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RuleId = "rule-1",
                RuleName = "Test Rule",
                SourceIp = normalizedEvent.SourceIp,
                EventCount = 2
            }
        };

        ruleEngineMock.Setup(x => x.EvaluateEventAsync(It.IsAny<EventEnvelope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);

        var consumeResult = new ConsumeResult<EventEnvelope>
        {
            Topic = "normalized-events",
            Partition = 1,
            Offset = 10,
            Message = new EventEnvelope
            {
                EventId = Guid.NewGuid(),
                Normalized = normalizedEvent
            }
        };

        await pipeline.EnqueueAsync(consumeResult, CancellationToken.None);

        await Task.Delay(400);
        await pipeline.DisposeAsync();

        alertRepositoryMock.Verify(x => x.PersistAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        alertPublisherMock.Verify(x => x.PublishAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
