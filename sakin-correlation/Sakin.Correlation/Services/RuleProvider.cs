using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public class RuleProvider : IRuleProvider
{
    private readonly CorrelationRulesOptions _options;
    private readonly ILogger<RuleProvider> _logger;

    public RuleProvider(IOptions<CorrelationRulesOptions> options, ILogger<RuleProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CorrelationRule>> GetRulesAsync(CancellationToken cancellationToken)
    {
        var ruleDirectory = Path.GetFullPath(_options.RulesDirectory);
        if (!Directory.Exists(ruleDirectory))
        {
            _logger.LogWarning("Rule directory {Directory} does not exist. Using built-in defaults.", ruleDirectory);
            return BuildDefaultRules();
        }

        var rules = new List<CorrelationRule>();
        var jsonOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };

        var files = Directory.EnumerateFiles(ruleDirectory, "*.json", SearchOption.TopDirectoryOnly).ToList();
        if (files.Count == 0)
        {
            _logger.LogWarning("No rule files found in {Directory}. Using built-in defaults.", ruleDirectory);
            return BuildDefaultRules();
        }

        foreach (var file in files)
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var fileRules = await JsonSerializer.DeserializeAsync<List<CorrelationRule>>(stream, jsonOptions, cancellationToken)
                               ?? new List<CorrelationRule>();

                rules.AddRange(fileRules.Where(rule => rule.Enabled));
                _logger.LogInformation("Loaded {RuleCount} rules from {File}", fileRules.Count, file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read rule file {File}. Skipping.", file);
            }
        }

        if (rules.Count == 0)
        {
            _logger.LogWarning("No rules loaded from files. Falling back to built-in defaults.");
            return BuildDefaultRules();
        }

        return rules;
    }

    private static IReadOnlyList<CorrelationRule> BuildDefaultRules() => new List<CorrelationRule>
    {
        new()
        {
            Id = "failed-auth-burst",
            Name = "Multiple Failed Authentications",
            Description = "Detects multiple failed authentication attempts from the same source",
            Severity = Severity.High,
            Enabled = true,
            MinEventCount = 5,
            TimeWindowSeconds = 300,
            Conditions = new List<RuleCondition>
            {
                new() { Field = "EventType", Operator = RuleOperator.Equals, Value = nameof(EventType.AuthenticationAttempt) }
            },
            GroupByFields = new List<string> { "SourceIp" },
            Tags = new List<string> { "brute-force", "authentication" }
        },
        new()
        {
            Id = "suspicious-port-scan",
            Name = "Suspicious Port Scanning Activity",
            Description = "Detects multiple connection attempts to different ports from same source",
            Severity = Severity.Medium,
            Enabled = true,
            MinEventCount = 10,
            TimeWindowSeconds = 600,
            Conditions = new List<RuleCondition>
            {
                new() { Field = "EventType", Operator = RuleOperator.Equals, Value = nameof(EventType.NetworkTraffic) }
            },
            GroupByFields = new List<string> { "SourceIp" },
            Tags = new List<string> { "port-scan", "reconnaissance" }
        },
        new()
        {
            Id = "http-attack-pattern",
            Name = "HTTP Attack Pattern Detection",
            Description = "Detects suspicious HTTP request patterns indicating potential attacks",
            Severity = Severity.High,
            Enabled = true,
            MinEventCount = 3,
            TimeWindowSeconds = 180,
            Conditions = new List<RuleCondition>
            {
                new() { Field = "EventType", Operator = RuleOperator.Equals, Value = nameof(EventType.HttpRequest) }
            },
            GroupByFields = new List<string> { "SourceIp" },
            Tags = new List<string> { "web-attack", "http" }
        }
    };
}
