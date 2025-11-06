namespace Sakin.Common.Configuration;

public class AlertLifecycleOptions
{
    public const string SectionName = "AlertLifecycle";

    public int DedupTtlMinutes { get; set; } = 60;

    public int StaleAlertThresholdHours { get; set; } = 24;

    public int BulkOperationLimit { get; set; } = 1000;

    public bool AutoResolutionEnabled { get; set; } = true;

    public string AuditTopic { get; set; } = "audit-log";
}
