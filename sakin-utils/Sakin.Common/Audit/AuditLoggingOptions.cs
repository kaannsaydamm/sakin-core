namespace Sakin.Common.Audit
{
    public class AuditLoggingOptions
    {
        public const string SectionName = "AuditLogging";

        public bool Enabled { get; set; } = true;

        public string Topic { get; set; } = "audit-log";

        public string? ServiceName { get; set; }

        public bool IncludePayload { get; set; } = true;
    }
}
