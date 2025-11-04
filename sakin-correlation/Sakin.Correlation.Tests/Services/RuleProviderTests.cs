using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Services;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class RuleProviderTests
{
    [Fact]
    public async Task GetRulesAsync_ReturnsDefaultRules()
    {
        var options = Options.Create(new CorrelationRulesOptions
        {
            RulesDirectory = "./non-existent-directory"
        });

        var provider = new RuleProvider(options, NullLogger<RuleProvider>.Instance);

        var rules = await provider.GetRulesAsync(CancellationToken.None);

        rules.Should().NotBeEmpty();
        rules.Should().AllSatisfy(rule =>
        {
            rule.Id.Should().NotBeEmpty();
            rule.Name.Should().NotBeEmpty();
            rule.Enabled.Should().BeTrue();
        });
    }
}
