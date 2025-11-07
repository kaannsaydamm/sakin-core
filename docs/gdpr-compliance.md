# GDPR Compliance Guide — S.A.K.I.N.

## Overview

This document outlines how S.A.K.I.N. complies with the General Data Protection Regulation (GDPR) and similar privacy regulations.

## Personal Data Collected

### Data Categories

S.A.K.I.N. processes the following categories of personal data:

| Category | Data Fields | Purpose | Legal Basis |
|----------|-------------|---------|-------------|
| **User Authentication** | Email, username, password hash | System access | Legitimate interest |
| **Audit Logs** | User ID, IP address, user agent | Security monitoring | Legitimate interest |
| **Alerts** | Source/destination IPs, usernames | Threat detection | Legitimate interest |
| **Assets** | Hostnames, IP addresses, owner email | Asset management | Legitimate interest |
| **Activity Logs** | Login times, API access patterns | Security analysis | Legitimate interest |

### Data Minimization

S.A.K.I.N. implements data minimization principles:

- Only collect data necessary for security functions
- Anonymize data where possible
- Aggregate data for analytics
- Short retention periods for raw events (30 days in ClickHouse)

## Lawful Basis for Processing

### Legitimate Interest

S.A.K.I.N. processes personal data under the legitimate interest basis:

**Legitimate Interest**: Cybersecurity and protection of information systems

**Balancing Test**:
- **Purpose**: Detect and respond to security threats
- **Necessity**: Cannot achieve purpose without processing IP addresses, usernames
- **Impact**: Minimal impact on individuals (internal security tool)
- **Safeguards**: Access controls, encryption, audit logging

**Documentation**: Legitimate Interest Assessment (LIA) documented separately

### Consent

For optional features (e.g., ML training on alert data):
- Explicit opt-in required
- Clear explanation of processing
- Easy withdrawal mechanism

## Data Subject Rights

### Right of Access (Article 15)

**API Endpoint**: `GET /api/gdpr/access`

**Implementation**:
```csharp
[HttpGet("gdpr/access")]
[Authorize(Policy = "ReadOwnData")]
public async Task<IActionResult> GetPersonalData(string userId)
{
    var data = new
    {
        Alerts = await _db.Alerts.Where(a => a.UserId == userId).ToListAsync(),
        AuditLogs = await _db.AuditEvents.Where(a => a.UserId == userId).ToListAsync(),
        Assets = await _db.Assets.Where(a => a.OwnerId == userId).ToListAsync()
    };
    
    return Ok(data);
}
```

**Response Time**: Within 30 days

### Right to Erasure (Article 17)

**API Endpoint**: `DELETE /api/gdpr/erase`

**Implementation**:
```csharp
[HttpDelete("gdpr/erase")]
[Authorize(Policy = "Admin")]
public async Task<IActionResult> ErasePersonalData(string userId)
{
    // Anonymize audit logs (cannot delete for security reasons)
    await _db.AuditEvents
        .Where(a => a.UserId == userId)
        .ExecuteUpdateAsync(a => a
            .SetProperty(e => e.UserId, "anonymized-" + Guid.NewGuid())
            .SetProperty(e => e.IpAddress, "0.0.0.0")
            .SetProperty(e => e.UserAgent, "anonymized"));
    
    // Delete user-generated content
    await _db.Alerts.Where(a => a.UserId == userId).ExecuteDeleteAsync();
    await _db.Assets.Where(a => a.OwnerId == userId).ExecuteDeleteAsync();
    
    await _auditService.LogAsync(new AuditEvent
    {
        Action = "gdpr_erasure",
        ResourceType = "user",
        ResourceId = userId,
        Status = "success"
    });
    
    return Ok();
}
```

**Limitations**:
- Audit logs anonymized (not deleted) for security/legal compliance
- Aggregated analytics retained (anonymized)
- Backups retained until next backup cycle

### Right to Rectification (Article 16)

**API Endpoint**: `PATCH /api/gdpr/rectify`

Users can update their personal data via standard API endpoints with proper authentication.

### Right to Restriction of Processing (Article 18)

**API Endpoint**: `POST /api/gdpr/restrict`

**Implementation**:
```csharp
[HttpPost("gdpr/restrict")]
public async Task<IActionResult> RestrictProcessing(string userId, string reason)
{
    await _db.Users
        .Where(u => u.Id == userId)
        .ExecuteUpdateAsync(u => u
            .SetProperty(e => e.ProcessingRestricted, true)
            .SetProperty(e => e.RestrictionReason, reason));
    
    // Mark user's data for no further processing (except storage)
    return Ok();
}
```

### Right to Data Portability (Article 20)

**API Endpoint**: `GET /api/gdpr/export`

**Implementation**:
```csharp
[HttpGet("gdpr/export")]
public async Task<IActionResult> ExportData(string userId)
{
    var data = await GetPersonalData(userId);
    
    // Export as JSON
    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    
    return File(Encoding.UTF8.GetBytes(json), "application/json", 
        $"sakin-personal-data-{userId}-{DateTime.UtcNow:yyyyMMdd}.json");
}
```

**Format**: JSON (machine-readable)

### Right to Object (Article 21)

Users can object to processing by:
1. Restricting processing (see above)
2. Requesting erasure
3. Disabling account

## Data Protection Measures

### Technical Measures

#### Encryption at Rest
- Database: PostgreSQL with TLS
- Redis: Password-protected, TLS optional
- Secrets: Kubernetes Secrets encrypted at rest
- Backups: Encrypted with GPG

#### Encryption in Transit
- API: HTTPS (TLS 1.2+)
- Service-to-service: mTLS
- Database connections: TLS enforced

#### Access Controls
- RBAC: Role-based access control
- Authentication: JWT tokens with short expiration
- Authorization: Permission checks on every request
- Audit: All access logged

#### Pseudonymization
- IP addresses: Hashed in analytics queries
- Usernames: Masked in logs (first + last char only)
- Email: Masked in error messages

### Organizational Measures

#### Data Protection Officer (DPO)
- **Contact**: dpo@company.com
- **Responsibilities**: GDPR compliance, breach response, DPA liaison

#### Privacy by Design
- Minimal data collection
- Short retention periods
- Access controls by default
- Privacy impact assessments (PIAs)

#### Staff Training
- Annual GDPR training
- Security awareness training
- Incident response training

#### Data Processing Agreements (DPAs)
- Cloud providers (AWS, Azure): DPA signed
- Subprocessors: List maintained and updated

## Data Retention

| Data Type | Retention Period | Reason |
|-----------|------------------|--------|
| **Raw Events** | 30 days | ClickHouse TTL |
| **Alerts** | 90 days (default) | Security investigation |
| **Audit Logs** | 2 years | Compliance requirement |
| **Aggregated Analytics** | Indefinite (anonymized) | Performance metrics |
| **Backups** | 90 days | Disaster recovery |
| **User Accounts** | Until deleted | Active use |

### Automated Deletion

```sql
-- ClickHouse: Auto-delete via TTL
ALTER TABLE sakin.events MODIFY TTL event_date + INTERVAL 30 DAY;

-- PostgreSQL: Scheduled cleanup job
CREATE OR REPLACE FUNCTION cleanup_old_alerts() RETURNS void AS $$
BEGIN
    DELETE FROM alerts WHERE created_at < NOW() - INTERVAL '90 days';
END;
$$ LANGUAGE plpgsql;

-- Cron job: Daily at 3 AM
SELECT cron.schedule('cleanup-alerts', '0 3 * * *', 'SELECT cleanup_old_alerts();');
```

## Breach Notification

### Detection
- Security monitoring alerts
- Anomaly detection
- User reports
- Audit log analysis

### Assessment (Within 24 hours)
- Determine data affected
- Number of individuals
- Sensitivity of data
- Likelihood of harm

### Notification (Within 72 hours)
- **Supervisory Authority**: Notify DPA
- **Data Subjects**: If high risk
- **Documentation**: Incident report

### Notification Template

**To DPA**:
```
Subject: Data Breach Notification - S.A.K.I.N.

Date: [Date]
Organization: [Company Name]
DPO Contact: dpo@company.com

1. Nature of Breach:
   - Type: [Unauthorized access / Data loss / etc.]
   - Date detected: [Date]
   - Data affected: [Description]

2. Categories of Data Subjects:
   - Number: [Approx. count]
   - Categories: [Employees / Customers / etc.]

3. Data Categories:
   - [IP addresses, usernames, etc.]

4. Likely Consequences:
   - [Risk assessment]

5. Measures Taken:
   - [Containment actions]
   - [Remediation steps]

6. Contact:
   - DPO: dpo@company.com
   - Phone: +X-XXX-XXX-XXXX
```

**To Data Subjects** (if high risk):
```
Subject: Security Incident Notification

Dear [User],

We are writing to inform you of a security incident that may have affected your personal data in our S.A.K.I.N. security monitoring system.

What Happened:
[Brief description]

What Data Was Affected:
[Specific data types]

What We're Doing:
[Remediation steps]

What You Should Do:
[Recommended actions]

For Questions:
Contact our DPO at dpo@company.com

Sincerely,
[Company] Security Team
```

## Subprocessors

S.A.K.I.N. uses the following subprocessors:

| Subprocessor | Service | Data Processed | Location | DPA |
|--------------|---------|----------------|----------|-----|
| AWS | Infrastructure | All data | EU (eu-west-1) | ✅ Signed |
| MaxMind | GeoIP | IP addresses | US | ✅ Signed |
| VirusTotal | Threat Intel | File hashes, IPs | US | ✅ Signed |
| Slack | Notifications | Alert summaries | US | ✅ Signed |

**Note**: Subprocessor list updated at `/docs/subprocessors.md`

## Data Protection Impact Assessment (DPIA)

A DPIA was conducted on 2025-01-01 for S.A.K.I.N.

**Conclusion**: Processing is necessary and proportionate. Risks mitigated through technical and organizational measures.

**DPIA Review**: Annually or when processing changes significantly.

## Cross-Border Data Transfers

### EU-US Transfers
- **Mechanism**: Standard Contractual Clauses (SCCs)
- **Supplementary Measures**: Encryption, access controls
- **Documentation**: SCCs signed with US subprocessors

### UK Transfers (Post-Brexit)
- **Mechanism**: UK GDPR adequacy decision or SCCs
- **Documentation**: Updated contracts

## User Rights Request Process

### How to Submit Request

**Email**: privacy@company.com  
**Subject**: GDPR Request - [Right Type]  
**Include**:
- Full name
- Email address
- User ID (if known)
- Type of request (access, erasure, etc.)

### Processing Timeline

1. **Receipt**: Acknowledged within 3 business days
2. **Verification**: Identity verification (2-5 business days)
3. **Processing**: Request fulfilled within 30 days
4. **Response**: Data provided or confirmation sent

### Verification Process

To prevent data disclosure to unauthorized parties:
- Email verification (confirmation link)
- Account credentials
- Additional identification (if needed)

## Consent Management

### Consent for ML Training

**Consent Text**:
```
[ ] I consent to my anonymized alert data being used to train 
    machine learning models to improve threat detection.
    
    You can withdraw this consent at any time without affecting 
    your use of S.A.K.I.N.
```

**Implementation**:
```csharp
public class UserPreferences
{
    public bool ConsentToMlTraining { get; set; }
    public DateTime? ConsentGivenAt { get; set; }
    public DateTime? ConsentWithdrawnAt { get; set; }
}
```

**Withdrawal**:
- Via UI: Settings > Privacy > Withdraw Consent
- Via API: `DELETE /api/user/consent/ml-training`

## Privacy Policy

Full privacy policy available at: `/docs/privacy-policy.md`

## Compliance Checklist

- [x] Lawful basis documented
- [x] Data subject rights implemented
- [x] Encryption at rest and in transit
- [x] Access controls (RBAC)
- [x] Audit logging
- [x] Data retention policies
- [x] Breach notification procedures
- [x] DPO appointed
- [x] DPA with cloud providers
- [x] DPIA conducted
- [x] Staff training
- [x] Privacy policy published
- [x] Consent management (where applicable)

## Contact

**Data Protection Officer**:  
Email: dpo@company.com  
Phone: +X-XXX-XXX-XXXX  
Address: [Company Address]

**Supervisory Authority** (Example: Germany):  
Die Bundesbeauftragte für den Datenschutz und die Informationsfreiheit (BfDI)  
Website: https://www.bfdi.bund.de/

---

**Last Updated**: 2025-01-06  
**Next Review**: 2025-07-06  
**Version**: 1.0
