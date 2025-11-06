using Microsoft.Extensions.Logging;
using Sakin.Common.Models.SOAR;
using Sakin.Common.Validation;
using System.IO;

namespace Sakin.SOAR.Services;

public interface IPlaybookRepository
{
    Task<PlaybookDefinition?> GetPlaybookAsync(string playbookId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PlaybookDefinition>> GetAllPlaybooksAsync(CancellationToken cancellationToken = default);
    Task ReloadPlaybooksAsync(CancellationToken cancellationToken = default);
    event EventHandler<PlaybookChangedEventArgs>? PlaybookChanged;
}

public class PlaybookChangedEventArgs : EventArgs
{
    public string PlaybookId { get; }
    public PlaybookDefinition? Playbook { get; }
    public PlaybookChangeType ChangeType { get; }

    public PlaybookChangedEventArgs(string playbookId, PlaybookDefinition? playbook, PlaybookChangeType changeType)
    {
        PlaybookId = playbookId;
        Playbook = playbook;
        ChangeType = changeType;
    }
}

public enum PlaybookChangeType
{
    Created,
    Updated,
    Deleted
}

public class PlaybookRepository : IPlaybookRepository, IDisposable
{
    private readonly ILogger<PlaybookRepository> _logger;
    private readonly IPlaybookValidator _validator;
    private readonly string _playbooksDirectory;
    private readonly FileSystemWatcher _fileWatcher;
    private readonly Dictionary<string, PlaybookDefinition> _playbooks;
    private readonly SemaphoreSlim _semaphore;

    public event EventHandler<PlaybookChangedEventArgs>? PlaybookChanged;

    public PlaybookRepository(
        ILogger<PlaybookRepository> logger,
        IPlaybookValidator validator,
        string playbooksDirectory = "playbooks")
    {
        _logger = logger;
        _validator = validator;
        _playbooksDirectory = playbooksDirectory;
        _playbooks = new Dictionary<string, PlaybookDefinition>();
        _semaphore = new SemaphoreSlim(1, 1);

        // Ensure playbooks directory exists
        Directory.CreateDirectory(_playbooksDirectory);

        // Setup file watcher for hot reload
        _fileWatcher = new FileSystemWatcher(_playbooksDirectory, "*.yaml")
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = true
        };

        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Deleted += OnFileDeleted;
        _fileWatcher.Renamed += OnFileRenamed;
    }

    public async Task<PlaybookDefinition?> GetPlaybookAsync(string playbookId, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _playbooks.TryGetValue(playbookId, out var playbook);
            return playbook;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IEnumerable<PlaybookDefinition>> GetAllPlaybooksAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return _playbooks.Values.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ReloadPlaybooksAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Reloading playbooks from directory: {Directory}", _playbooksDirectory);

            var previousPlaybooks = new Dictionary<string, PlaybookDefinition>(_playbooks);
            _playbooks.Clear();

            var yamlFiles = Directory.GetFiles(_playbooksDirectory, "*.yaml", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(_playbooksDirectory, "*.yml", SearchOption.AllDirectories));

            foreach (var filePath in yamlFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    var validationResult = _validator.ValidatePlaybook(content);

                    if (!validationResult.IsValid)
                    {
                        _logger.LogWarning("Invalid playbook file {FilePath}: {Error}", filePath, validationResult.ErrorMessage);
                        continue;
                    }

                    var playbook = _validator.ParsePlaybook(content);
                    if (playbook != null)
                    {
                        _playbooks[playbook.Id] = playbook;

                        // Check if this is an update
                        if (previousPlaybooks.TryGetValue(playbook.Id, out var previousPlaybook))
                        {
                            PlaybookChanged?.Invoke(this, new PlaybookChangedEventArgs(playbook.Id, playbook, PlaybookChangeType.Updated));
                        }
                        else
                        {
                            PlaybookChanged?.Invoke(this, new PlaybookChangedEventArgs(playbook.Id, playbook, PlaybookChangeType.Created));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load playbook from file: {FilePath}", filePath);
                }
            }

            // Check for deleted playbooks
            foreach (var kvp in previousPlaybooks)
            {
                if (!_playbooks.ContainsKey(kvp.Key))
                {
                    PlaybookChanged?.Invoke(this, new PlaybookChangedEventArgs(kvp.Key, null, PlaybookChangeType.Deleted));
                }
            }

            _logger.LogInformation("Loaded {Count} playbooks successfully", _playbooks.Count);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (Path.GetExtension(e.FullPath).ToLowerInvariant() is not (".yaml" or ".yml"))
            return;

        _logger.LogDebug("Playbook file changed: {FilePath}", e.FullPath);
        await ReloadPlaybooksAsync();
    }

    private async void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (Path.GetExtension(e.FullPath).ToLowerInvariant() is not (".yaml" or ".yml"))
            return;

        _logger.LogDebug("Playbook file deleted: {FilePath}", e.FullPath);
        await ReloadPlaybooksAsync();
    }

    private async void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (Path.GetExtension(e.FullPath).ToLowerInvariant() is not (".yaml" or ".yml"))
            return;

        _logger.LogDebug("Playbook file renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
        await ReloadPlaybooksAsync();
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _semaphore?.Dispose();
    }
}