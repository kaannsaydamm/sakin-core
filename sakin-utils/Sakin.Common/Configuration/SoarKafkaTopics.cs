namespace Sakin.Common.Configuration;

public class SoarKafkaTopics
{
    public string AlertActions { get; set; } = "sakin-alerts-actions";
    public string AgentCommand { get; set; } = "sakin-agent-command";
    public string AgentResult { get; set; } = "sakin-agent-result";
    public string AuditLog { get; set; } = "sakin-audit-log";
    public string AlertActionsDlq { get; set; } = "sakin-alerts-actions-dlq";
    public string AgentCommandDlq { get; set; } = "sakin-agent-command-dlq";
}