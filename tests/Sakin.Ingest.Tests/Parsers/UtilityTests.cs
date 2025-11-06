using FluentAssertions;
using Sakin.Ingest.Parsers.Utilities;
using Xunit;

namespace Sakin.Ingest.Tests.Parsers;

public class TimeParserTests
{
    [Theory]
    [InlineData("2024-01-15T10:31:00Z")]
    [InlineData("2024-01-15T10:31:00.123Z")]
    [InlineData("2024-01-15T10:31:00.123456Z")]
    public void TryParse_Iso8601Formats_ReturnsTrue(string timestamp)
    {
        var result = TimeParser.TryParse(timestamp, out var parsed);

        result.Should().BeTrue();
        parsed.Should().Be(new DateTime(2024, 1, 15, 10, 31, 0, DateTimeKind.Utc), because: $"timestamp: {timestamp}");
    }

    [Theory]
    [InlineData("Jan 15 10:31:00")]
    [InlineData("Jan  5 10:31:00")]
    public void TryParse_CommonFormats_ReturnsTrue(string timestamp)
    {
        var result = TimeParser.TryParse(timestamp, out var parsed);

        result.Should().BeTrue();
        parsed.Year.Should().BeGreaterThan(2000);
    }

    [Fact]
    public void TryParse_InvalidTimestamp_ReturnsFalse()
    {
        var result = TimeParser.TryParse("not-a-timestamp", out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryParse_NullOrEmpty_ReturnsFalse()
    {
        TimeParser.TryParse(null, out _).Should().BeFalse();
        TimeParser.TryParse(string.Empty, out _).Should().BeFalse();
        TimeParser.TryParse("   ", out _).Should().BeFalse();
    }
}

public class IpParserTests
{
    [Theory]
    [InlineData("192.168.1.1", "192.168.1.1")]
    [InlineData("10.0.0.1", "10.0.0.1")]
    [InlineData("203.0.113.45", "203.0.113.45")]
    public void TryValidateAndNormalize_ValidIps_ReturnsTrue(string input, string expected)
    {
        var result = IpParser.TryValidateAndNormalize(input, out var normalized);

        result.Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("256.256.256.256")]
    public void TryValidateAndNormalize_InvalidIps_ReturnsFalse(string input)
    {
        var result = IpParser.TryValidateAndNormalize(input, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryValidateAndNormalize_NullOrEmpty_ReturnsFalse()
    {
        IpParser.TryValidateAndNormalize(null, out _).Should().BeFalse();
        IpParser.TryValidateAndNormalize(string.Empty, out _).Should().BeFalse();
        IpParser.TryValidateAndNormalize("   ", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("203.0.113.45", false)]
    public void IsPrivate_CorrectlyIdentifiesPrivateIps(string ip, bool expected)
    {
        var result = IpParser.IsPrivate(ip);

        result.Should().Be(expected);
    }
}

public class GrokMatcherTests
{
    [Fact]
    public void TryMatch_SimplePattern_ExtractsGroups()
    {
        var matcher = new GrokMatcher(@"(?<method>\w+)\s+(?<path>\S+)\s+(?<status>\d{3})");
        var input = "GET /api/users 200";

        var result = matcher.TryMatch(input, out var groups);

        result.Should().BeTrue();
        groups.Should().ContainKey("method");
        groups["method"].Should().Be("GET");
        groups.Should().ContainKey("path");
        groups["path"].Should().Be("/api/users");
        groups.Should().ContainKey("status");
        groups["status"].Should().Be("200");
    }

    [Fact]
    public void TryMatch_NoMatch_ReturnsFalse()
    {
        var matcher = new GrokMatcher(@"(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})");
        var input = "not-an-ip";

        var result = matcher.TryMatch(input, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void Match_WithMatch_ReturnsGroups()
    {
        var matcher = new GrokMatcher(@"(?<user>\w+):(?<pass>\S+)");
        var input = "admin:password123";

        var result = matcher.Match(input);

        result.Should().NotBeNull();
        result.Should().ContainKey("user");
        result["user"].Should().Be("admin");
        result.Should().ContainKey("pass");
        result["pass"].Should().Be("password123");
    }

    [Fact]
    public void Match_NoMatch_ReturnsNull()
    {
        var matcher = new GrokMatcher(@"(?<number>\d+)");
        var input = "no numbers here";

        var result = matcher.Match(input);

        result.Should().BeNull();
    }
}

public class RegexCacheTests
{
    [Fact]
    public void GetOrCreate_SamePattern_ReturnsCachedRegex()
    {
        var pattern = @"(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})";

        var regex1 = RegexCache.GetOrCreate(pattern);
        var regex2 = RegexCache.GetOrCreate(pattern);

        regex1.Should().BeSameAs(regex2);
    }

    [Fact]
    public void GetOrCreate_DifferentPatterns_ReturnsDifferentRegex()
    {
        var pattern1 = @"(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})";
        var pattern2 = @"(?<email>\S+@\S+)";

        var regex1 = RegexCache.GetOrCreate(pattern1);
        var regex2 = RegexCache.GetOrCreate(pattern2);

        regex1.Should().NotBeSameAs(regex2);
    }

    [Fact]
    public void GetOrCreate_CompiledRegex_MatchesCorrectly()
    {
        var pattern = @"(?<method>\w+)\s+(?<path>/\S+)";
        var regex = RegexCache.GetOrCreate(pattern);

        var match = regex.Match("GET /api/users");

        match.Success.Should().BeTrue();
        match.Groups["method"].Value.Should().Be("GET");
        match.Groups["path"].Value.Should().Be("/api/users");
    }
}
