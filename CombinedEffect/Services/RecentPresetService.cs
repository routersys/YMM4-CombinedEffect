using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Windows;

namespace CombinedEffect.Services;

internal sealed class RecentPresetService : IRecentPresetService, IDisposable
{
    private readonly string _filePath;
    private ImmutableList<Guid> _recentIds = ImmutableList<Guid>.Empty;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly AsyncDebouncer _debouncer = new();
    private bool _disposed;

    public RecentPresetService()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
        _filePath = Path.Combine(assemblyDir, Constants.DirectoryPresets, Constants.FileRecentIds);
        _ = Task.Run(LoadAsync);
    }

    public Task<IReadOnlyList<Guid>> GetRecentIdsAsync()
    {
        IReadOnlyList<Guid> result;
        lock (_lock) result = _recentIds;
        return Task.FromResult(result);
    }

    public void Add(Guid id)
    {
        lock (_lock)
        {
            var newIds = _recentIds.Remove(id).Insert(0, id);
            if (newIds.Count > 10)
                newIds = newIds.RemoveRange(10, newIds.Count - 10);
            _recentIds = newIds;
        }
        SaveAsync();
    }

    public void Remove(Guid id)
    {
        bool changed = false;
        lock (_lock)
        {
            if (_recentIds.Contains(id))
            {
                _recentIds = _recentIds.Remove(id);
                changed = true;
            }
        }
        if (changed) SaveAsync();
    }

    private async Task LoadAsync()
    {
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var content = AtomicFileWriter.ReadVerified(_filePath);
            if (content is null) return;
            var ids = JsonConvert.DeserializeObject<List<Guid>>(content);
            if (ids is not null)
            {
                lock (_lock)
                {
                    _recentIds = [.. ids];
                }
            }
        }
        catch { }
        finally { _ioLock.Release(); }
    }

    private void SaveAsync()
    {
        ImmutableList<Guid> currentIds;
        lock (_lock) currentIds = _recentIds;

        _debouncer.DebounceAsync("recent", TimeSpan.FromMilliseconds(500), async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (directory is not null && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(_filePath, JsonConvert.SerializeObject(currentIds)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show($"{Texts.Error_DiskIO}\n{ex.Message}", Texts.Dialog_Title, MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                _ioLock.Release();
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debouncer.Dispose();
        _ioLock.Dispose();
    }
}