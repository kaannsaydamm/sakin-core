using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sakin.Correlation.Services;

public interface ITimeOfDayService
{
    bool IsOffHours(DateTimeOffset timestamp);
    TimeSpan GetBusinessHoursStart();
    TimeSpan GetBusinessHoursEnd();
}

public class TimeOfDayService : ITimeOfDayService
{
    private readonly ILogger<TimeOfDayService> _logger;
    private readonly RiskScoringConfiguration _config;
    private readonly TimeSpan _businessStart;
    private readonly TimeSpan _businessEnd;

    public TimeOfDayService(
        ILogger<TimeOfDayService> logger,
        IOptions<RiskScoringConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
        
        // Parse business hours (format: "09:00-17:00")
        var parts = _config.BusinessHours.Split('-');
        if (parts.Length != 2)
        {
            _logger.LogWarning("Invalid BusinessHours format: {BusinessHours}, using default 09:00-17:00", _config.BusinessHours);
            _businessStart = TimeSpan.FromHours(9);
            _businessEnd = TimeSpan.FromHours(17);
        }
        else
        {
            if (!TimeSpan.TryParse(parts[0].Trim(), out _businessStart) ||
                !TimeSpan.TryParse(parts[1].Trim(), out _businessEnd))
            {
                _logger.LogWarning("Could not parse BusinessHours: {BusinessHours}, using default 09:00-17:00", _config.BusinessHours);
                _businessStart = TimeSpan.FromHours(9);
                _businessEnd = TimeSpan.FromHours(17);
            }
        }
    }

    public bool IsOffHours(DateTimeOffset timestamp)
    {
        var timeOfDay = timestamp.TimeOfDay;
        
        // Check if time is outside business hours
        var isOffHours = timeOfDay < _businessStart || timeOfDay > _businessEnd;
        
        _logger.LogDebug("Timestamp {Timestamp} ({TimeOfDay}) is {OffHours} business hours ({Start}-{End})", 
            timestamp, timeOfDay, isOffHours ? "outside" : "within", _businessStart, _businessEnd);
        
        return isOffHours;
    }

    public TimeSpan GetBusinessHoursStart() => _businessStart;
    public TimeSpan GetBusinessHoursEnd() => _businessEnd;
}