using Microsoft.AspNetCore.Mvc;
using Sakin.Common.Models;
using Sakin.Panel.Api.Services;

namespace Sakin.Panel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _assetService;
    private readonly ILogger<AssetsController> _logger;

    public AssetsController(IAssetService assetService, ILogger<AssetsController> logger)
    {
        _assetService = assetService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AssetListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAssets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? assetType = null,
        [FromQuery] string? criticality = null,
        [FromQuery] string? owner = null,
        [FromQuery] string? tag = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AssetListRequest
        {
            Page = page,
            PageSize = pageSize,
            Search = search,
            Owner = owner,
            Tag = tag
        };

        if (!string.IsNullOrWhiteSpace(assetType))
        {
            if (Enum.TryParse<AssetType>(assetType, true, out var parsedType))
            {
                request.AssetType = parsedType;
            }
            else
            {
                return ValidationProblem(new ValidationProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid asset type",
                    Detail = $"'{assetType}' is not a valid asset type."
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(criticality))
        {
            if (Enum.TryParse<AssetCriticality>(criticality, true, out var parsedCriticality))
            {
                request.Criticality = parsedCriticality;
            }
            else
            {
                return ValidationProblem(new ValidationProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid criticality",
                    Detail = $"'{criticality}' is not a valid criticality level."
                });
            }
        }

        var result = await _assetService.ListAssetsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Asset), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsset(Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await _assetService.GetAssetByIdAsync(id, cancellationToken);
        
        if (asset == null)
        {
            return NotFound();
        }

        return Ok(asset);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Asset), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsset(
        [FromBody] AssetCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var asset = await _assetService.CreateAssetAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetAsset), new { id = asset.Id }, asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating asset");
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error creating asset",
                Detail = ex.Message
            });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Asset), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsset(
        Guid id,
        [FromBody] AssetUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var asset = await _assetService.UpdateAssetAsync(id, request, cancellationToken);
            
            if (asset == null)
            {
                return NotFound();
            }

            return Ok(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset {AssetId}", id);
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error updating asset",
                Detail = ex.Message
            });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsset(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _assetService.DeleteAssetAsync(id, cancellationToken);
        
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("import")]
    [ProducesResponseType(typeof(AssetImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportAssets(
        IFormFile csvFile,
        CancellationToken cancellationToken = default)
    {
        if (csvFile == null || csvFile.Length == 0)
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "No file provided",
                Detail = "Please select a CSV file to import."
            });
        }

        if (!Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid file format",
                Detail = "Please provide a CSV file."
            });
        }

        try
        {
            var result = await _assetService.ImportAssetsAsync(csvFile, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing assets from CSV");
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error importing assets",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("lookup/ip/{ipAddress}")]
    [ProducesResponseType(typeof(Asset), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupByIp(string ipAddress, CancellationToken cancellationToken = default)
    {
        var asset = await _assetService.GetAssetByIpAsync(ipAddress, cancellationToken);
        
        if (asset == null)
        {
            return NotFound();
        }

        return Ok(asset);
    }

    [HttpGet("lookup/hostname/{hostname}")]
    [ProducesResponseType(typeof(Asset), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupByHostname(string hostname, CancellationToken cancellationToken = default)
    {
        var asset = await _assetService.GetAssetByHostnameAsync(hostname, cancellationToken);
        
        if (asset == null)
        {
            return NotFound();
        }

        return Ok(asset);
    }
}