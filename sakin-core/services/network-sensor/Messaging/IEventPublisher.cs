using Sakin.Common.Models;

namespace Sakin.Core.Sensor.Messaging
{
    public interface IEventPublisher
    {
        bool Enqueue(NetworkEvent networkEvent);
        PublishMetrics Metrics { get; }
        string TopicName { get; }
    }

    public class PublishMetrics
    {
        private long _attempted;
        private long _delivered;
        private long _failed;

        public void IncrementAttempted(long by = 1) => Interlocked.Add(ref _attempted, by);
        public void IncrementDelivered(long by = 1) => Interlocked.Add(ref _delivered, by);
        public void IncrementFailed(long by = 1) => Interlocked.Add(ref _failed, by);

        public long Attempted => Interlocked.Read(ref _attempted);
        public long Delivered => Interlocked.Read(ref _delivered);
        public long Failed => Interlocked.Read(ref _failed);
        public double SuccessRate => Attempted == 0 ? 0 : (double)Delivered / Attempted * 100.0;
    }
}
