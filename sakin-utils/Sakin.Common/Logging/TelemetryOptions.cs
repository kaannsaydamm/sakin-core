using System;

namespace Sakin.Common.Logging
{
    public class TelemetryOptions
    {
        public const string SectionName = "Telemetry";

        public string? ServiceName { get; set; }

        public string? Environment { get; set; }

        public bool EnableTracing { get; set; } = true;

        public bool EnableMetrics { get; set; } = true;

        public bool EnableLogExport { get; set; } = true;

        public bool EnableConsoleJson { get; set; } = true;

        public string OtlpEndpoint { get; set; } = "http://jaeger:4317";

        public string PrometheusScrapeEndpoint { get; set; } = "/metrics";

        public string? ActivitySourceName { get; set; }

        public string? MeterName { get; set; }

        public double TraceSamplerProbability { get; set; } = 1.0;

        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);
    }
}
