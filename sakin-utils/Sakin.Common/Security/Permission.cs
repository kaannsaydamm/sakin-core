namespace Sakin.Common.Security;

[Flags]
public enum Permission
{
    None = 0,
    
    // Alert permissions
    ReadAlerts = 1 << 0,
    WriteAlerts = 1 << 1,
    AcknowledgeAlerts = 1 << 2,
    BulkAlertOperations = 1 << 3,
    
    // Rule permissions
    ReadRules = 1 << 4,
    WriteRules = 1 << 5,
    
    // Playbook permissions
    ReadPlaybooks = 1 << 6,
    WritePlaybooks = 1 << 7,
    ExecutePlaybooks = 1 << 8,
    
    // Asset permissions
    ReadAssets = 1 << 9,
    WriteAssets = 1 << 10,
    
    // Config permissions
    ReadConfig = 1 << 11,
    WriteConfig = 1 << 12,
    
    // User management
    ManageUsers = 1 << 13,
    
    // Audit logs
    ReadAuditLogs = 1 << 14,
    
    // Agent commands
    ExecuteAgentCommands = 1 << 15,
    
    // System administration
    SystemAdmin = 1 << 16,
    
    // Threat intelligence
    ReadThreatIntel = 1 << 17,
    WriteThreatIntel = 1 << 18,
    
    // All permissions
    All = ~0
}

public static class RolePermissions
{
    public static Permission GetPermissions(Role role)
    {
        return role switch
        {
            Role.Admin => Permission.All,
            
            Role.SocManager => Permission.ReadAlerts | Permission.WriteAlerts | 
                               Permission.AcknowledgeAlerts | Permission.BulkAlertOperations |
                               Permission.ReadRules | Permission.ReadPlaybooks | 
                               Permission.ExecutePlaybooks | Permission.ReadAssets |
                               Permission.WriteAssets | Permission.ReadThreatIntel |
                               Permission.ReadAuditLogs,
            
            Role.Analyst => Permission.ReadAlerts | Permission.WriteAlerts | 
                            Permission.AcknowledgeAlerts | Permission.ReadRules |
                            Permission.ReadPlaybooks | Permission.WritePlaybooks |
                            Permission.ReadAssets | Permission.ReadThreatIntel,
            
            Role.ReadOnly => Permission.ReadAlerts | Permission.ReadRules | 
                             Permission.ReadPlaybooks | Permission.ReadAssets |
                             Permission.ReadThreatIntel,
            
            Role.Agent => Permission.ExecuteAgentCommands,
            
            _ => Permission.None
        };
    }
    
    public static bool HasPermission(Role role, Permission permission)
    {
        var rolePermissions = GetPermissions(role);
        return (rolePermissions & permission) == permission;
    }
}
