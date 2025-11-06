using FluentAssertions;
using Sakin.Common.Models;
using Sakin.Ingest.Parsers;
using Xunit;

namespace Sakin.Ingest.Tests.Parsers;

public class ParserRegistryTests
{
    [Fact]
    public void Register_AddsParser_CanBeRetrieved()
    {
        var registry = new ParserRegistry();
        var parser = new WindowsEventLogParser();

        registry.Register(parser);

        registry.TryGetParser("windows-eventlog", out var retrieved).Should().BeTrue();
        retrieved.Should().Be(parser);
    }

    [Fact]
    public void Register_NullParser_ThrowsArgumentNullException()
    {
        var registry = new ParserRegistry();

        var act = () => registry.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_MultiipleParsers_AllCanBeRetrieved()
    {
        var registry = new ParserRegistry();
        var winParser = new WindowsEventLogParser();
        var syslogParser = new SyslogParser();
        var apacheParser = new ApacheAccessLogParser();

        registry.Register(winParser);
        registry.Register(syslogParser);
        registry.Register(apacheParser);

        registry.TryGetParser("windows-eventlog", out var p1).Should().BeTrue();
        registry.TryGetParser("syslog", out var p2).Should().BeTrue();
        registry.TryGetParser("apache", out var p3).Should().BeTrue();

        p1.Should().Be(winParser);
        p2.Should().Be(syslogParser);
        p3.Should().Be(apacheParser);
    }

    [Fact]
    public void TryGetParser_UnregisteredSourceType_ReturnsFalse()
    {
        var registry = new ParserRegistry();

        var result = registry.TryGetParser("unknown-type", out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryGetParser_CaseInsensitive()
    {
        var registry = new ParserRegistry();
        var parser = new WindowsEventLogParser();

        registry.Register(parser);

        registry.TryGetParser("WINDOWS-EVENTLOG", out var retrieved).Should().BeTrue();
        registry.TryGetParser("Windows-EventLog", out var retrieved2).Should().BeTrue();
    }

    [Fact]
    public void GetParser_ReturnsParser()
    {
        var registry = new ParserRegistry();
        var parser = new WindowsEventLogParser();

        registry.Register(parser);

        var result = registry.GetParser("windows-eventlog");

        result.Should().Be(parser);
    }

    [Fact]
    public void GetParser_UnregisteredSourceType_ReturnsNull()
    {
        var registry = new ParserRegistry();

        var result = registry.GetParser("unknown");

        result.Should().BeNull();
    }

    [Fact]
    public void GetRegisteredSourceTypes_ReturnsAllRegisteredTypes()
    {
        var registry = new ParserRegistry();

        registry.Register(new WindowsEventLogParser());
        registry.Register(new SyslogParser());
        registry.Register(new ApacheAccessLogParser());
        registry.Register(new FortinetLogParser());

        var types = registry.GetRegisteredSourceTypes().ToList();

        types.Should().Contain("windows-eventlog");
        types.Should().Contain("syslog");
        types.Should().Contain("apache");
        types.Should().Contain("fortinet");
        types.Should().HaveCount(4);
    }

    [Fact]
    public void Register_DuplicateSourceType_OverwritesPreviousParser()
    {
        var registry = new ParserRegistry();
        var parser1 = new WindowsEventLogParser();
        var parser2 = new SyslogParser();

        registry.Register(parser1);
        registry.Register(parser1); // Register again

        registry.GetParser("windows-eventlog").Should().Be(parser1);
    }
}
