using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Correlation.Parsers;
using System.Text.Json;

namespace Sakin.Correlation.Services;

public class RuleLoaderServiceV2 : BackgroundService, IRuleLoaderServiceV2
{
    private readonly IRuleParser _ruleParser;
    private readonly ILogger<RuleLoaderServiceV2> _logger;
    private readonly RulesOptions _options;
    private readonly List<CorrelationRule> _rules = new();
    private readonly List<CorrelationRuleV2> _rulesV2 = new();

    public IReadOnlyList<CorrelationRule> Rules => _rules.AsReadOnly();
    public IReadOnlyList<CorrelationRuleV2> RulesV2 => _rulesV2.AsReadOnly();

    public RuleLoaderServiceV2(
        IRuleParser ruleParser,
        IOptions<RulesOptions> options,
        ILogger<RuleLoaderServiceV2> logger)
    {
        _ruleParser = ruleParser;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rule loader service V2 starting...");
        
        // Load rules initially
        await LoadRulesAsync(stoppingToken);
        
        // Keep the service alive but don't do periodic reloading for now
        // In the future, we could add file watching for hot reload
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Rule loader service V2 stopping...");
        }
    }

    public async Task LoadRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Loading rules from {RulesPath}", _options.RulesPath);
            
            if (!Directory.Exists(_options.RulesPath))
            {
                _logger.LogWarning("Rules directory not found: {RulesPath}", _options.RulesPath);
                return;
            }

            // Load legacy rules
            var legacyRules = await _ruleParser.ParseRulesFromDirectoryAsync(_options.RulesPath);
            
            // Load V2 rules
            var v2Rules = await LoadV2RulesAsync(_options.RulesPath);
            
            _rules.Clear();
            _rules.AddRange(legacyRules);
            
            _rulesV2.Clear();
            _rulesV2.AddRange(v2Rules);
            
            _logger.LogInformation("Successfully loaded {LegacyRuleCount} legacy rules and {V2RuleCount} V2 rules", 
                _rules.Count, _rulesV2.Count);
            
            foreach (var rule in _rulesV2)
            {
                _logger.LogDebug("Loaded V2 rule: {RuleId} - {RuleName} (Enabled: {Enabled})", 
                    rule.Id, rule.Name, rule.Enabled);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rules from {RulesPath}", _options.RulesPath);
            throw;
        }
    }

    public async Task ReloadRulesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reloading rules...");
        await LoadRulesAsync(cancellationToken);
    }

    private async Task<List<CorrelationRuleV2>> LoadV2RulesAsync(string rulesPath)
    {
        var rules = new List<CorrelationRuleV2>();
        
        var jsonFiles = Directory.GetFiles(rulesPath, "*.json", SearchOption.AllDirectories);
        
        foreach (var filePath in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var rule = JsonSerializer.Deserialize<CorrelationRuleV2>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (rule != null && !string.IsNullOrEmpty(rule.Id))
                {
                    rules.Add(rule);
                    _logger.LogDebug("Loaded V2 rule from {FilePath}: {RuleId}", filePath, rule.Id);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Failed to parse V2 rule from {FilePath}, trying legacy parser", filePath);
                // This might be a legacy rule, which is fine
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading V2 rule from {FilePath}", filePath);
            }
        }
        
        return rules;
    }
}