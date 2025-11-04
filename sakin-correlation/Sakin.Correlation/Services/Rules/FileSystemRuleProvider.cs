using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Correlation.Parsers;

namespace Sakin.Correlation.Services.Rules;

public class FileSystemRuleProvider : IRuleProvider, IDisposable
{
    private readonly IRuleParser _ruleParser;
    private readonly RulesSettings _settings;
    private readonly ILogger<FileSystemRuleProvider> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private ImmutableArray<CorrelationRule> _rules = ImmutableArray<CorrelationRule>.Empty;
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly object _debounceSync = new();

    public FileSystemRuleProvider(
        IRuleParser ruleParser,
        IOptions<RulesSettings> options,
        ILogger<FileSystemRuleProvider> logger)
    {
        _ruleParser = ruleParser;
        _settings = options.Value;
        _logger = logger;

        var rulesPath = GetRulesPath();
        Directory.CreateDirectory(rulesPath);

        Task.Run(() => ReloadAsync(CancellationToken.None)).GetAwaiter().GetResult();

        if (_settings.ReloadOnChange)
        {
            InitializeWatcher(rulesPath);
        }
    }

    public IReadOnlyCollection<CorrelationRule> GetRules() => _rules;

    public Task ReloadAsync(CancellationToken cancellationToken = default)
        => ReloadInternalAsync(cancellationToken);

    private string GetRulesPath()
    {
        var path = _settings.RulesPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/configs/rules/";
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private void InitializeWatcher(string rulesPath)
    {
        _watcher = new FileSystemWatcher(rulesPath, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnRuleFileChanged;
        _watcher.Created += OnRuleFileChanged;
        _watcher.Deleted += OnRuleFileChanged;
        _watcher.Renamed += OnRuleFileChanged;
    }

    private void OnRuleFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Rule file change detected: {ChangeType} {Path}", e.ChangeType, e.FullPath);
        DebounceReload();
    }

    private void DebounceReload()
    {
        lock (_debounceSync)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(async _ =>
            {
                try
                {
                    await ReloadInternalAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reload rules after change notification");
                }
            }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
        }
    }

    private async Task ReloadInternalAsync(CancellationToken cancellationToken)
    {
        await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rulesPath = GetRulesPath();
            if (!Directory.Exists(rulesPath))
            {
                _rules = ImmutableArray<CorrelationRule>.Empty;
                return;
            }

            var files = Directory.GetFiles(rulesPath, "*.json", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                _rules = ImmutableArray<CorrelationRule>.Empty;
                _logger.LogWarning("No correlation rules found at {RulesPath}", rulesPath);
                return;
            }

            var rules = new List<CorrelationRule>(files.Length);
            foreach (var file in files)
            {
                try
                {
                    var rule = await _ruleParser.ParseRuleFromFileAsync(file, cancellationToken).ConfigureAwait(false);
                    rules.Add(rule);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse correlation rule from {File}", file);
                }
            }

            _rules = rules.ToImmutableArray();
            _logger.LogInformation("Loaded {RuleCount} correlation rules", _rules.Length);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
        _reloadLock.Dispose();
    }
}
