using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Services;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class TimeOfDayServiceTests
{
    private readonly Mock<ILogger<TimeOfDayService>> _mockLogger;
    private readonly TimeOfDayService _service;

    public TimeOfDayServiceTests()
    {
        _mockLogger = new Mock<ILogger<TimeOfDayService>>();
    }

    [Fact]
    public void Constructor_WithValidBusinessHours_ShouldParseCorrectly()
    {
        // Arrange
        var config = new RiskScoringConfiguration
        {
            BusinessHours = "09:00-17:00"
        };
        var configOptions = Options.Create(config);

        // Act
        var service = new TimeOfDayService(_mockLogger.Object, configOptions);

        // Assert
        Assert.Equal(TimeSpan.FromHours(9), service.GetBusinessHoursStart());
        Assert.Equal(TimeSpan.FromHours(17), service.GetBusinessHoursEnd());
    }

    [Fact]
    public void Constructor_WithInvalidBusinessHours_ShouldUseDefaults()
    {
        // Arrange
        var config = new RiskScoringConfiguration
        {
            BusinessHours = "invalid-format"
        };
        var configOptions = Options.Create(config);

        // Act
        var service = new TimeOfDayService(_mockLogger.Object, configOptions);

        // Assert
        Assert.Equal(TimeSpan.FromHours(9), service.GetBusinessHoursStart());
        Assert.Equal(TimeSpan.FromHours(17), service.GetBusinessHoursEnd());
    }

    [Theory]
    [InlineData("2024-01-01T08:59:59Z", true)]  // Before business hours
    [InlineData("2024-01-01T09:00:00Z", false)] // Start of business hours
    [InlineData("2024-01-01T12:00:00Z", false)] // Middle of business hours
    [InlineData("2024-01-01T17:00:00Z", false)] // End of business hours
    [InlineData("2024-01-01T17:00:01Z", true)]  // After business hours
    [InlineData("2024-01-01T23:00:00Z", true)]  // Late evening
    [InlineData("2024-01-01T02:00:00Z", true)]  // Early morning
    public void IsOffHours_WithStandardBusinessHours_ShouldReturnCorrectly(string timestampString, bool expectedIsOffHours)
    {
        // Arrange
        var config = new RiskScoringConfiguration
        {
            BusinessHours = "09:00-17:00"
        };
        var configOptions = Options.Create(config);
        var service = new TimeOfDayService(_mockLogger.Object, configOptions);
        
        var timestamp = DateTimeOffset.Parse(timestampString);

        // Act
        var result = service.IsOffHours(timestamp);

        // Assert
        Assert.Equal(expectedIsOffHours, result);
    }

    [Theory]
    [InlineData("22:00-06:00", "2024-01-01T23:00:00Z", false)] // Night shift hours
    [InlineData("22:00-06:00", "2024-01-01T05:59:59Z", false)] // Before end of night shift
    [InlineData("22:00-06:00", "2024-01-01T06:00:00Z", true)]  // After night shift
    [InlineData("22:00-06:00", "2024-01-01T21:59:59Z", true)]  // Before night shift
    public void IsOffHours_WithNightShiftBusinessHours_ShouldReturnCorrectly(
        string businessHours, string timestampString, bool expectedIsOffHours)
    {
        // Arrange
        var config = new RiskScoringConfiguration
        {
            BusinessHours = businessHours
        };
        var configOptions = Options.Create(config);
        var service = new TimeOfDayService(_mockLogger.Object, configOptions);
        
        var timestamp = DateTimeOffset.Parse(timestampString);

        // Act
        var result = service.IsOffHours(timestamp);

        // Assert
        Assert.Equal(expectedIsOffHours, result);
    }

    [Theory]
    [InlineData("08:00-20:00", "2024-01-01T07:59:59Z", true)]   // Just before
    [InlineData("08:00-20:00", "2024-01-01T08:00:00Z", false)]  // Start
    [InlineData("08:00-20:00", "2024-01-01T20:00:00Z", false)]  // End
    [InlineData("08:00-20:00", "2024-01-01T20:00:01Z", true)]   // Just after
    public void IsOffHours_WithExtendedBusinessHours_ShouldReturnCorrectly(
        string businessHours, string timestampString, bool expectedIsOffHours)
    {
        // Arrange
        var config = new RiskScoringConfiguration
        {
            BusinessHours = businessHours
        };
        var configOptions = Options.Create(config);
        var service = new TimeOfDayService(_mockLogger.Object, configOptions);
        
        var timestamp = DateTimeOffset.Parse(timestampString);

        // Act
        var result = service.IsOffHours(timestamp);

        // Assert
        Assert.Equal(expectedIsOffHours, result);
    }
}