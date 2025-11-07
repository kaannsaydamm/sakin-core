namespace Sakin.Common.Audit;

public interface IAuditService
{
    Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<List<AuditEvent>> SearchAsync(AuditSearchCriteria criteria, CancellationToken cancellationToken = default);
}

public class AuditSearchCriteria
{
    public string? User { get; set; }
    public string? Action { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}
