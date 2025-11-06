using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Correlation.Parsers;

namespace Sakin.Correlation.Services;

public class RuleLoaderService : BackgroundService, IRuleLoaderService
{
    private readonly IRuleParser _ruleParser;
    private readonly ILogger<RuleLoaderService> _logger;
    private readonly RulesOptions _options;
    private readonly List<CorrelationRule> _rules = new();

    public IReadOnlyList<CorrelationRule> Rules => _rules.AsReadOnly();

    public RuleLoaderService(
        IRuleParser ruleParser,
        IOptions<RulesOptions> options,
        ILogger<RuleLoaderService> logger)
    {
        _ruleParser = ruleParser;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rule loader service starting...");
        
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
            _logger.LogInformation("Rule loader service stopping...");
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

            var rules = await _ruleParser.ParseRulesFromDirectoryAsync(_options.RulesPath);
            
            _rules.Clear();
            _rules.AddRange(rules);
            
            _logger.LogInformation("Successfully loaded {RuleCount} rules", _rules.Count);
            
            foreach (var rule in _rules)
            {
                _logger.LogDebug("Loaded rule: {RuleId} - {RuleName} (Enabled: {Enabled})", 
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
}