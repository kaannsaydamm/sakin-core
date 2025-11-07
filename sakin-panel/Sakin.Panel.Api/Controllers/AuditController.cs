using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sakin.Common.Audit;
using Sakin.Common.Security;

namespace Sakin.Panel.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize(Policy = "ReadAuditLogs")]
    public async Task<IActionResult> Search(
        [FromQuery] string? user,
        [FromQuery] string? action,
        [FromQuery] string? resourceType,
        [FromQuery] string? resourceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        var criteria = new AuditSearchCriteria
        {
            User = user,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            FromDate = from,
            ToDate = to,
            Limit = Math.Min(limit, 1000),
            Offset = offset
        };

        var results = await _auditService.SearchAsync(criteria);
        
        return Ok(new
        {
            data = results,
            count = results.Count,
            limit = criteria.Limit,
            offset = criteria.Offset
        });
    }
}
