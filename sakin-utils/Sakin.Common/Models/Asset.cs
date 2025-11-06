using System.ComponentModel.DataAnnotations;

namespace Sakin.Common.Models;

public enum AssetCriticality
{
    Low,
    Medium,
    High,
    Critical
}

public enum AssetType
{
    Host,
    Service,
    Database,
    Firewall,
    NetworkDevice,
    IoT,
    Other
}

public class Asset
{
    public Guid Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? IpAddress { get; set; }
    
    public string? Hostname { get; set; }
    
    [Required]
    public AssetType AssetType { get; set; }
    
    [Required]
    public AssetCriticality Criticality { get; set; }
    
    public string? Owner { get; set; }
    
    public List<string> Tags { get; set; } = new();
    
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}

public class AssetCreateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? IpAddress { get; set; }
    
    public string? Hostname { get; set; }
    
    [Required]
    public AssetType AssetType { get; set; }
    
    [Required]
    public AssetCriticality Criticality { get; set; }
    
    public string? Owner { get; set; }
    
    public List<string> Tags { get; set; } = new();
    
    public string? Description { get; set; }
}

public class AssetUpdateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? IpAddress { get; set; }
    
    public string? Hostname { get; set; }
    
    [Required]
    public AssetType AssetType { get; set; }
    
    [Required]
    public AssetCriticality Criticality { get; set; }
    
    public string? Owner { get; set; }
    
    public List<string> Tags { get; set; } = new();
    
    public string? Description { get; set; }
}

public class AssetListRequest
{
    public int Page { get; set; } = 1;
    
    public int PageSize { get; set; } = 20;
    
    public string? Search { get; set; }
    
    public AssetType? AssetType { get; set; }
    
    public AssetCriticality? Criticality { get; set; }
    
    public string? Owner { get; set; }
    
    public string? Tag { get; set; }
}

public class AssetListResponse
{
    public List<Asset> Assets { get; set; } = new();
    
    public int TotalCount { get; set; }
    
    public int Page { get; set; }
    
    public int PageSize { get; set; }
    
    public int TotalPages { get; set; }
}

public class AssetImportRequest
{
    public string Hostname { get; set; } = string.Empty;
    
    public string? Ip { get; set; }
    
    public AssetType AssetType { get; set; }
    
    public AssetCriticality Criticality { get; set; }
    
    public string? Owner { get; set; }
    
    public string? Tags { get; set; } // Comma-separated tags
}

public class AssetImportResult
{
    public int TotalRecords { get; set; }
    
    public int SuccessfulImports { get; set; }
    
    public int FailedImports { get; set; }
    
    public List<string> Errors { get; set; } = new();
}