using System;

namespace Sakin.Agents.Windows.Models
{
    public class EventLogEntryData
    {
        public string LogName { get; set; } = string.Empty;

        public int EventId { get; set; }

        public long? RecordId { get; set; }

        public string? ProviderName { get; set; }

        public string? EventName { get; set; }

        public string? LevelDisplayName { get; set; }

        public int? Level { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public string MachineName { get; set; } = string.Empty;

        public string? UserName { get; set; }

        public string RawXml { get; set; } = string.Empty;
    }
}
