using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Correlation.Parsers;
using System.Collections.Concurrent;

namespace Sakin.Correlation.Services;

public class RuleLoaderService : BackgroundService, IRuleLoaderService
{
    private readonly IRuleParser _ruleParser;
    private readonly ILogger<RuleLoaderService> _logger;
    private readonly RulesOptions _options;
    private readonly List<CorrelationRule> _rules = new();
    private readonly SemaphoreSlim _reloadSemaphore = new(1, 1);
    private FileSystemWatcher? _fileWatcher;
    private Timer? _debounceTimer;
    private readonly object _debounceLock = new();

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
        
        // Set up file watching if enabled
        if (_options.ReloadOnChange && Directory.Exists(_options.RulesPath))
        {
            SetupFileWatcher();
            _logger.LogInformation("Hot-reload enabled for rules directory: {RulesPath}", _options.RulesPath);
        }
        else if (_options.ReloadOnChange)
        {
            _logger.LogWarning("Hot-reload enabled but rules directory not found: {RulesPath}", _options.RulesPath);
        }
        
        // Keep the service alive
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Rule loader service stopping...");
        }
    }

    private void SetupFileWatcher()
    {
        _fileWatcher = new FileSystemWatcher(_options.RulesPath)
        {
            Filter = "*.json",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Deleted += OnFileChanged;
        _fileWatcher.Renamed += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File system change detected: {ChangeType} - {FilePath}", e.ChangeType, e.Name);
        
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                async _ => await TriggerReloadAsync(),
                null,
                _options.DebounceMilliseconds,
                Timeout.Infinite
            );
        }
    }

    private async Task TriggerReloadAsync()
    {
        try
        {
            await ReloadRulesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during triggered reload");
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
        await _reloadSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            _logger.LogInformation("Reloading rules...");
            
            if (!Directory.Exists(_options.RulesPath))
            {
                _logger.LogWarning("Rules directory not found during reload: {RulesPath}", _options.RulesPath);
                return;
            }

            // Parse and validate new rules
            List<CorrelationRule> newRules;
            try
            {
                newRules = await _ruleParser.ParseRulesFromDirectoryAsync(_options.RulesPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse rules during reload. Keeping previous valid rule set.");
                return;
            }

            // Calculate and log differences
            var oldRuleIds = _rules.Select(r => r.Id).ToHashSet();
            var newRuleIds = newRules.Select(r => r.Id).ToHashSet();

            var addedRules = newRuleIds.Except(oldRuleIds).ToList();
            var removedRules = oldRuleIds.Except(newRuleIds).ToList();
            var commonRules = oldRuleIds.Intersect(newRuleIds).ToList();

            var modifiedRules = new List<string>();
            foreach (var ruleId in commonRules)
            {
                var oldRule = _rules.First(r => r.Id == ruleId);
                var newRule = newRules.First(r => r.Id == ruleId);
                
                // Simple comparison - in production might want more sophisticated change detection
                if (!RulesEqual(oldRule, newRule))
                {
                    modifiedRules.Add(ruleId);
                }
            }

            // Log differences
            if (addedRules.Count > 0)
            {
                _logger.LogInformation("Added rules: {AddedRules}", string.Join(", ", addedRules));
            }
            
            if (removedRules.Count > 0)
            {
                _logger.LogInformation("Removed rules: {RemovedRules}", string.Join(", ", removedRules));
            }
            
            if (modifiedRules.Count > 0)
            {
                _logger.LogInformation("Modified rules: {ModifiedRules}", string.Join(", ", modifiedRules));
            }

            if (addedRules.Count == 0 && removedRules.Count == 0 && modifiedRules.Count == 0)
            {
                _logger.LogInformation("No rule changes detected");
                return;
            }

            // Atomically update rules
            _rules.Clear();
            _rules.AddRange(newRules);
            
            _logger.LogInformation("Successfully reloaded {RuleCount} rules", _rules.Count);
        }
        finally
        {
            _reloadSemaphore.Release();
        }
    }

    private bool RulesEqual(CorrelationRule rule1, CorrelationRule rule2)
    {
        // Simple equality check based on serialization
        // In production, you might want a more sophisticated comparison
        var json1 = _ruleParser.SerializeRule(rule1);
        var json2 = _ruleParser.SerializeRule(rule2);
        return json1 == json2;
    }

    public override void Dispose()
    {
        _debounceTimer?.Dispose();
        _fileWatcher?.Dispose();
        _reloadSemaphore?.Dispose();
        base.Dispose();
    }
}