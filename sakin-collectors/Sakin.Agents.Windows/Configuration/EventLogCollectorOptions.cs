using System.Collections.Generic;

namespace Sakin.Agents.Windows.Configuration
{
    public class EventLogCollectorOptions
    {
        public const string SectionName = "EventLogs";

        public bool Enabled { get; set; } = true;

        public EventLogMode Mode { get; set; } = EventLogMode.RealTime;

        public int PollIntervalMs { get; set; } = 5000;

        public int PollInterval
        {
            get => PollIntervalMs;
            set => PollIntervalMs = value;
        }

        public int BatchSize { get; set; } = 100;

        public List<string> LogNames { get; set; } = new();

        public List<EventLogSubscriptionOptions> Logs { get; set; } = new();
    }

    public class EventLogSubscriptionOptions
    {
        public string Name { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        public List<int> EventIds { get; set; } = new();

        public string? Query { get; set; }
    }

    public enum EventLogMode
    {
        RealTime,
        Batch
    }
}
