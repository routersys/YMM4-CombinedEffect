using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Models.History;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Windows;

namespace CombinedEffect.Services;

internal sealed class HistoryRepository : IHistoryRepository, IDisposable
{
    private static readonly JsonSerializerSettings Settings = new() { Formatting = Formatting.Indented };
    private readonly string _historyDirectory;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly AsyncDebouncer _debouncer = new();
    private readonly ConcurrentDictionary<Guid, List<HistoryBranch>> _branchCache = new();
    private readonly ConcurrentDictionary<Guid, Dictionary<Guid, HistorySnapshot>> _snapshotCache = new();
    private bool _disposed;

    public HistoryRepository()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppDomain.CurrentDomain.BaseDirectory;
        _historyDirectory = Path.Combine(assemblyDir, Constants.DirectoryHistory);
        Directory.CreateDirectory(_historyDirectory);
    }

    private string GetPresetDirectory(Guid presetId)
    {
        var dir = Path.Combine(_historyDirectory, presetId.ToString("D"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string GetBranchesPath(Guid presetId) =>
        Path.Combine(GetPresetDirectory(presetId), "branches.json");

    private string GetSnapshotPath(Guid presetId, Guid snapshotId) =>
        Path.Combine(GetPresetDirectory(presetId), $"{snapshotId:D}.json");

    public async Task<List<HistoryBranch>> LoadBranchesAsync(Guid presetId)
    {
        if (_branchCache.TryGetValue(presetId, out var cached))
            return cached;

        return await Task.Run(async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_branchCache.TryGetValue(presetId, out var inner))
                    return inner;
                var content = AtomicFileWriter.ReadVerified(GetBranchesPath(presetId));
                if (content is null) return [];
                var branches = JsonConvert.DeserializeObject<List<HistoryBranch>>(content, Settings) ?? [];
                _branchCache[presetId] = branches;
                return branches;
            }
            catch { return []; }
            finally { _ioLock.Release(); }
        }).ConfigureAwait(false);
    }

    public void SaveBranches(Guid presetId, List<HistoryBranch> branches)
    {
        _branchCache[presetId] = branches;
        var json = JsonConvert.SerializeObject(branches, Settings);
        _debouncer.DebounceAsync($"branches_{presetId:N}", TimeSpan.FromMilliseconds(300), async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await AtomicFileWriter.WriteAtomicAsync(GetBranchesPath(presetId), json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show($"{Texts.Error_DiskIO}\n{ex.Message}", Texts.Dialog_Title, MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally { _ioLock.Release(); }
        });
    }

    public async Task<HistorySnapshot?> LoadSnapshotAsync(Guid presetId, Guid snapshotId)
    {
        if (_snapshotCache.TryGetValue(presetId, out var dict) && dict.TryGetValue(snapshotId, out var cached))
            return cached;

        return await Task.Run(async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var content = AtomicFileWriter.ReadVerified(GetSnapshotPath(presetId, snapshotId));
                if (content is null) return null;
                var snap = JsonConvert.DeserializeObject<HistorySnapshot>(content, Settings);
                if (snap is not null)
                {
                    var d = _snapshotCache.GetOrAdd(presetId, _ => new Dictionary<Guid, HistorySnapshot>());
                    d[snapshotId] = snap;
                }
                return snap;
            }
            catch { return null; }
            finally { _ioLock.Release(); }
        }).ConfigureAwait(false);
    }

    public void SaveSnapshot(Guid presetId, HistorySnapshot snapshot)
    {
        var dict = _snapshotCache.GetOrAdd(presetId, _ => new Dictionary<Guid, HistorySnapshot>());
        dict[snapshot.Id] = snapshot;

        var json = JsonConvert.SerializeObject(snapshot, Settings);
        _debouncer.DebounceAsync($"snapshot_{snapshot.Id:N}", TimeSpan.FromMilliseconds(300), async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await AtomicFileWriter.WriteAtomicAsync(GetSnapshotPath(presetId, snapshot.Id), json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show($"{Texts.Error_DiskIO}\n{ex.Message}", Texts.Dialog_Title, MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally { _ioLock.Release(); }
        });
    }

    public async Task<List<HistorySnapshot>> LoadAllSnapshotsAsync(Guid presetId)
    {
        if (_snapshotCache.TryGetValue(presetId, out var dict))
            return [.. dict.Values.OrderByDescending(s => s.Timestamp)];

        return await Task.Run<List<HistorySnapshot>>(async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var dir = GetPresetDirectory(presetId);
                var files = Directory.GetFiles(dir, "*.json")
                    .Where(f => !f.EndsWith("branches.json", StringComparison.OrdinalIgnoreCase));
                var snapshots = new List<HistorySnapshot>();
                var newDict = new Dictionary<Guid, HistorySnapshot>();
                foreach (var file in files)
                {
                    var content = AtomicFileWriter.ReadVerified(file);
                    if (content is null) continue;
                    try
                    {
                        var snap = JsonConvert.DeserializeObject<HistorySnapshot>(content, Settings);
                        if (snap is not null)
                        {
                            snapshots.Add(snap);
                            newDict[snap.Id] = snap;
                        }
                    }
                    catch { }
                }
                _snapshotCache[presetId] = newDict;
                return [.. snapshots.OrderByDescending(s => s.Timestamp)];
            }
            finally { _ioLock.Release(); }
        }).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debouncer.Dispose();
        _ioLock.Dispose();
    }
}