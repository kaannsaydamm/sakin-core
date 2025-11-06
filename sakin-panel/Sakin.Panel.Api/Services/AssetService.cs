using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Sakin.Common.Models;
using Sakin.Common.Cache;

namespace Sakin.Panel.Api.Services;

public class AssetService : IAssetService
{
    private readonly string _connectionString;
    private readonly IRedisClient _redisClient;
    private readonly ILogger<AssetService> _logger;

    public AssetService(IConfiguration configuration, IRedisClient redisClient, ILogger<AssetService> logger)
    {
        _connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Postgres connection string not configured");
        _redisClient = redisClient;
        _logger = logger;
    }

    public async Task<Asset?> GetAssetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT id, name, ip_address as IpAddress, hostname, asset_type as AssetType, criticality as Criticality, 
                   owner, tags, description, created_at as CreatedAt, updated_at as UpdatedAt
            FROM Assets 
            WHERE id = @Id";

        var result = await connection.QueryAsync<Asset>(sql, new { Id = id });
        return result.FirstOrDefault();
    }

    public async Task<Asset?> GetAssetByIpAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT id, name, ip_address as IpAddress, hostname, asset_type as AssetType, criticality as Criticality, 
                   owner, tags, description, created_at as CreatedAt, updated_at as UpdatedAt
            FROM Assets 
            WHERE ip_address = @IpAddress";

        var result = await connection.QueryAsync<Asset>(sql, new { IpAddress = ipAddress });
        return result.FirstOrDefault();
    }

    public async Task<Asset?> GetAssetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT id, name, ip_address as IpAddress, hostname, asset_type as AssetType, criticality as Criticality, 
                   owner, tags, description, created_at as CreatedAt, updated_at as UpdatedAt
            FROM Assets 
            WHERE hostname = @Hostname";

        var result = await connection.QueryAsync<Asset>(sql, new { Hostname = hostname });
        return result.FirstOrDefault();
    }

    public async Task<AssetListResponse> ListAssetsAsync(AssetListRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            conditions.Add("(name ILIKE @Search OR ip_address::text ILIKE @Search OR hostname ILIKE @Search)");
            parameters.Add("Search", $"%{request.Search}%");
        }

        if (request.AssetType.HasValue)
        {
            conditions.Add("asset_type = @AssetType");
            parameters.Add("AssetType", request.AssetType.Value.ToString().ToLowerInvariant());
        }

        if (request.Criticality.HasValue)
        {
            conditions.Add("criticality = @Criticality");
            parameters.Add("Criticality", request.Criticality.Value.ToString().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(request.Owner))
        {
            conditions.Add("owner ILIKE @Owner");
            parameters.Add("Owner", $"%{request.Owner}%");
        }

        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            conditions.Add("@Tag = ANY(tags)");
            parameters.Add("Tag", request.Tag);
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        // Get total count
        const string countSql = $"SELECT COUNT(*) FROM Assets {whereClause}";
        var totalCount = await connection.QuerySingleAsync<int>(countSql, parameters);

        // Get paginated results
        var offset = (request.Page - 1) * request.PageSize;
        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", offset);

        const string dataSql = $@"
            SELECT id, name, ip_address as IpAddress, hostname, asset_type as AssetType, criticality as Criticality, 
                   owner, tags, description, created_at as CreatedAt, updated_at as UpdatedAt
            FROM Assets 
            {whereClause}
            ORDER BY created_at DESC
            LIMIT @PageSize OFFSET @Offset";

        var assets = (await connection.QueryAsync<Asset>(dataSql, parameters)).ToList();

        return new AssetListResponse
        {
            Assets = assets,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
        };
    }

    public async Task<Asset> CreateAssetAsync(AssetCreateRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        
        const string sql = @"
            INSERT INTO Assets (name, ip_address, hostname, asset_type, criticality, owner, tags, description)
            VALUES (@Name, @IpAddress, @Hostname, @AssetType, @Criticality, @Owner, @Tags, @Description)
            RETURNING id, name, ip_address as IpAddress, hostname, asset_type as AssetType, criticality as Criticality, 
                      owner, tags, description, created_at as CreatedAt, updated_at as UpdatedAt";

        var parameters = new
        {
            Name = request.Name,
            IpAddress = request.IpAddress,
            Hostname = request.Hostname,
            AssetType = request.AssetType.ToString().ToLowerInvariant(),
            Criticality = request.Criticality.ToString().ToLowerInvariant(),
            Owner = request.Owner,
            Tags = request.Tags?.ToArray() ?? new string[0],
            Description = request.Description
        };

        var asset = await connection.QuerySingleAsync<Asset>(sql, parameters);

        // Publish cache invalidation
        await PublishAssetUpdateAsync(asset);

        return asset;
    }

    public async Task<Asset?> UpdateAssetAsync(Guid id, AssetUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        
        const string sql = @"
            UPDATE Assets 
            SET name = @Name, ip_address = @IpAddress, hostname = @Hostname, 
                asset_type = @AssetType, criticality = @Criticality, owner = @Owner, 
                tags = @Tags, description = @Description, updated_at = CURRENT_TIMESTAMP
            WHERE id = @Id
            RETURNING id, name, ip_address as IpAddress, hostname, asset_type as AssetType, criticality as Criticality, 
                      owner, tags, description, created_at as CreatedAt, updated_at as UpdatedAt";

        var parameters = new
        {
            Id = id,
            Name = request.Name,
            IpAddress = request.IpAddress,
            Hostname = request.Hostname,
            AssetType = request.AssetType.ToString().ToLowerInvariant(),
            Criticality = request.Criticality.ToString().ToLowerInvariant(),
            Owner = request.Owner,
            Tags = request.Tags?.ToArray() ?? new string[0],
            Description = request.Description
        };

        var asset = await connection.QuerySingleOrDefaultAsync<Asset>(sql, parameters);
        
        if (asset != null)
        {
            // Publish cache invalidation
            await PublishAssetUpdateAsync(asset);
        }

        return asset;
    }

    public async Task<bool> DeleteAssetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        
        // Get asset before deletion for cache invalidation
        var asset = await GetAssetByIdAsync(id, cancellationToken);
        if (asset == null) return false;

        const string sql = "DELETE FROM Assets WHERE id = @Id";
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });

        if (rowsAffected > 0)
        {
            // Publish cache invalidation
            await PublishAssetDeleteAsync(asset);
        }

        return rowsAffected > 0;
    }

    public async Task<AssetImportResult> ImportAssetsAsync(IFormFile csvFile, CancellationToken cancellationToken = default)
    {
        var result = new AssetImportResult();
        
        try
        {
            using var stream = csvFile.OpenReadStream();
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            });

            var records = new List<AssetImportRequest>();
            await csv.ReadAsync();
            csv.ReadHeader();
            
            while (await csv.ReadAsync())
            {
                try
                {
                    var record = new AssetImportRequest
                    {
                        Hostname = csv.GetField("hostname") ?? string.Empty,
                        Ip = csv.GetField("ip"),
                        AssetType = Enum.Parse<AssetType>(csv.GetField("asset_type") ?? "other", true),
                        Criticality = Enum.Parse<AssetCriticality>(csv.GetField("criticality") ?? "low", true),
                        Owner = csv.GetField("owner"),
                        Tags = csv.GetField("tags")
                    };
                    records.Add(record);
                    result.TotalRecords++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error parsing row {csv.Context.Parser.Row}: {ex.Message}");
                    result.FailedImports++;
                }
            }

            // Process records in batches
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var record in records)
            {
                try
                {
                    var createRequest = new AssetCreateRequest
                    {
                        Name = record.Hostname,
                        Hostname = record.Hostname,
                        IpAddress = record.Ip,
                        AssetType = record.AssetType,
                        Criticality = record.Criticality,
                        Owner = record.Owner,
                        Tags = string.IsNullOrWhiteSpace(record.Tags) ? new List<string>() : record.Tags.Split(',').Select(t => t.Trim()).ToList(),
                        Description = $"Imported from CSV on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
                    };

                    await CreateAssetAsync(createRequest, cancellationToken);
                    result.SuccessfulImports++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error importing asset '{record.Hostname}': {ex.Message}");
                    result.FailedImports++;
                }
            }

            // Publish batch update notification
            await PublishBatchUpdateAsync();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error processing CSV file: {ex.Message}");
            _logger.LogError(ex, "Error importing assets from CSV");
        }

        return result;
    }

    private async Task PublishAssetUpdateAsync(Asset asset)
    {
        try
        {
            var message = new
            {
                action = "asset_updated",
                asset_id = asset.Id.ToString(),
                asset_name = asset.Name,
                ip_address = asset.IpAddress,
                hostname = asset.Hostname,
                timestamp = DateTime.UtcNow
            };

            await _redisClient.PublishAsync("sakin:cache:notify", JsonSerializer.Serialize(message));
            _logger.LogDebug("Published asset update notification for {AssetName}", asset.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing asset update notification");
        }
    }

    private async Task PublishAssetDeleteAsync(Asset asset)
    {
        try
        {
            var message = new
            {
                action = "asset_deleted",
                asset_id = asset.Id.ToString(),
                asset_name = asset.Name,
                ip_address = asset.IpAddress,
                hostname = asset.Hostname,
                timestamp = DateTime.UtcNow
            };

            await _redisClient.PublishAsync("sakin:cache:notify", JsonSerializer.Serialize(message));
            _logger.LogDebug("Published asset delete notification for {AssetName}", asset.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing asset delete notification");
        }
    }

    private async Task PublishBatchUpdateAsync()
    {
        try
        {
            var message = new
            {
                action = "batch_update",
                timestamp = DateTime.UtcNow
            };

            await _redisClient.PublishAsync("sakin:cache:notify", JsonSerializer.Serialize(message));
            _logger.LogDebug("Published batch update notification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing batch update notification");
        }
    }
}