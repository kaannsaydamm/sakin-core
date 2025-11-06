using Sakin.Common.Models;
using Sakin.Common.Cache;

namespace Sakin.Panel.Api.Services;

public interface IAssetService
{
    Task<Asset?> GetAssetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<Asset?> GetAssetByIpAsync(string ipAddress, CancellationToken cancellationToken = default);
    
    Task<Asset?> GetAssetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
    
    Task<AssetListResponse> ListAssetsAsync(AssetListRequest request, CancellationToken cancellationToken = default);
    
    Task<Asset> CreateAssetAsync(AssetCreateRequest request, CancellationToken cancellationToken = default);
    
    Task<Asset?> UpdateAssetAsync(Guid id, AssetUpdateRequest request, CancellationToken cancellationToken = default);
    
    Task<bool> DeleteAssetAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<AssetImportResult> ImportAssetsAsync(IFormFile csvFile, CancellationToken cancellationToken = default);
}