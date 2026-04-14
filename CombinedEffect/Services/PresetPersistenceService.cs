using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;
using CombinedEffect.Settings;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace CombinedEffect.Services;

internal sealed class PresetPersistenceService : IPresetPersistenceService, IDisposable
{
    private static readonly JsonSerializerSettings Settings = new() { Formatting = Formatting.Indented };

    private readonly ILoggerService _logger;
    private readonly IMainDiskRepository _mainDisk;
    private readonly IBackupStorageManager _backupStorage;
    private readonly AsyncDebouncer _debouncer = new();
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    private readonly ConcurrentDictionary<Guid, PresetBackupMeta> _presetMetaCache;
    private bool _disposed;

    public PresetPersistenceService(
        ILoggerService logger,
        IMainDiskRepository mainDisk,
        IBackupStorageManager backupStorage)
    {
        _logger = logger;
        _mainDisk = mainDisk;
        _backupStorage = backupStorage;
        _presetMetaCache = new ConcurrentDictionary<Guid, PresetBackupMeta>(
            CombinedEffectSettings.Instance.Presets.ToDictionary(m => m.Id));
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private void SyncMetaToSettings()
    {
        CombinedEffectSettings.Instance.Presets = _presetMetaCache.Values.ToArray();
        CombinedEffectSettings.Instance.Save();
    }

    public async Task<GroupRegistry> LoadGroupRegistryAsync()
    {
        return await Task.Run(async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var settings = CombinedEffectSettings.Instance;
                var diskData = _mainDisk.ReadRegistry();
                var hostJson = _backupStorage.ReadRegistry();

                var metaTime = settings.RegistryBackupTimestamp;
                var metaHash = settings.RegistryBackupHash;

                var diskHash = diskData != null ? ComputeHash(diskData.Value.Json) : null;
                var hostHash = hostJson != null ? ComputeHash(hostJson) : null;

                var diskMatchesMeta = diskHash != null && diskHash == metaHash;
                var hostMatchesMeta = hostHash != null && hostHash == metaHash;

                string? finalContent = null;
                var healDisk = false;
                var healHost = false;
                var healMeta = false;

                if (diskMatchesMeta)
                {
                    finalContent = diskData!.Value.Json;
                    if (!hostMatchesMeta) healHost = true;
                }
                else if (hostMatchesMeta)
                {
                    finalContent = hostJson;
                    if (!diskMatchesMeta) healDisk = true;
                }
                else
                {
                    if (diskHash != null && hostHash != null)
                    {
                        finalContent = diskData!.Value.Timestamp > metaTime ? diskData.Value.Json : hostJson;
                    }
                    else if (diskHash != null) finalContent = diskData!.Value.Json;
                    else if (hostHash != null) finalContent = hostJson;

                    healMeta = true;
                    healDisk = true;
                    healHost = true;
                }

                if (finalContent == null) return CreateDefaultRegistry();

                var registry = JsonConvert.DeserializeObject<GroupRegistry>(finalContent, Settings) ?? CreateDefaultRegistry();

                if (healDisk && finalContent != diskData?.Json)
                {
                    _logger.LogInfo("Restored Registry to Disk");
                    await _mainDisk.WriteRegistryAsync(metaTime, finalContent).ConfigureAwait(false);
                }
                if (healHost && finalContent != hostJson)
                {
                    _logger.LogInfo("Restored Registry Backup Storage");
                    await _backupStorage.WriteRegistryAsync(finalContent).ConfigureAwait(false);
                }
                if (healMeta || (healDisk && diskData == null) || (healHost && hostJson == null))
                {
                    settings.RegistryBackupHash = ComputeHash(finalContent);
                    settings.RegistryBackupTimestamp = DateTime.UtcNow.Ticks;
                    settings.Save();
                }

                return registry;
            }
            catch (Exception ex)
            {
                _logger.LogError("LoadRegistry", ex);
                return CreateDefaultRegistry();
            }
            finally { _ioLock.Release(); }
        }).ConfigureAwait(false);
    }

    public void SaveGroupRegistry(GroupRegistry registry)
    {
        var json = JsonConvert.SerializeObject(registry, Settings);
        _debouncer.DebounceAsync("registry", TimeSpan.FromMilliseconds(300), async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var time = DateTime.UtcNow.Ticks;
                var hash = ComputeHash(json);

                await _mainDisk.WriteRegistryAsync(time, json).ConfigureAwait(false);
                await _backupStorage.WriteRegistryAsync(json).ConfigureAwait(false);

                var settings = CombinedEffectSettings.Instance;
                settings.RegistryBackupTimestamp = time;
                settings.RegistryBackupHash = hash;
                settings.Save();
            }
            catch (Exception ex)
            {
                _logger.LogError("SaveRegistry", ex);
            }
            finally { _ioLock.Release(); }
        });
    }

    public async Task<EffectPreset?> LoadPresetAsync(Guid id)
    {
        return await Task.Run(async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var diskData = _mainDisk.ReadPreset(id);
                _presetMetaCache.TryGetValue(id, out var meta);
                var hostJson = _backupStorage.ReadPreset(id);

                var metaTime = meta?.Timestamp ?? 0;
                var metaHash = meta?.Hash;

                var diskHash = diskData != null ? ComputeHash(diskData.Value.Json) : null;
                var hostHash = hostJson != null ? ComputeHash(hostJson) : null;

                var diskMatchesMeta = diskHash != null && diskHash == metaHash;
                var hostMatchesMeta = hostHash != null && hostHash == metaHash;

                string? finalContent = null;
                var healDisk = false;
                var healHost = false;
                var healMeta = false;

                if (diskMatchesMeta)
                {
                    finalContent = diskData!.Value.Json;
                    if (!hostMatchesMeta) healHost = true;
                }
                else if (hostMatchesMeta)
                {
                    finalContent = hostJson;
                    if (!diskMatchesMeta) healDisk = true;
                }
                else
                {
                    if (diskHash != null && hostHash != null)
                    {
                        finalContent = diskData!.Value.Timestamp > metaTime ? diskData.Value.Json : hostJson;
                    }
                    else if (diskHash != null) finalContent = diskData!.Value.Json;
                    else if (hostHash != null) finalContent = hostJson;

                    healMeta = true;
                    healDisk = true;
                    healHost = true;
                }

                if (finalContent == null) return null;

                var preset = JsonConvert.DeserializeObject<EffectPreset>(finalContent, Settings);

                if (healDisk && finalContent != diskData?.Json)
                {
                    _logger.LogInfo($"Restored Preset {id} to Disk");
                    await _mainDisk.WritePresetAsync(id, metaTime, finalContent).ConfigureAwait(false);
                }
                if (healHost && finalContent != hostJson)
                {
                    _logger.LogInfo($"Restored Preset {id} Backup Storage");
                    await _backupStorage.WritePresetAsync(id, finalContent).ConfigureAwait(false);
                }
                if (healMeta || (healDisk && diskData == null) || (healHost && hostJson == null))
                {
                    _presetMetaCache[id] = new PresetBackupMeta
                    {
                        Id = id,
                        Timestamp = DateTime.UtcNow.Ticks,
                        Hash = ComputeHash(finalContent)
                    };
                    SyncMetaToSettings();
                }

                return preset;
            }
            catch (Exception ex)
            {
                _logger.LogError($"LoadPreset {id}", ex);
                return null;
            }
            finally { _ioLock.Release(); }
        }).ConfigureAwait(false);
    }

    public void SavePreset(EffectPreset preset)
    {
        var json = JsonConvert.SerializeObject(preset, Settings);
        _debouncer.DebounceAsync($"preset_{preset.Id:N}", TimeSpan.FromMilliseconds(300), async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var time = DateTime.UtcNow.Ticks;
                var hash = ComputeHash(json);

                await _mainDisk.WritePresetAsync(preset.Id, time, json).ConfigureAwait(false);
                await _backupStorage.WritePresetAsync(preset.Id, json).ConfigureAwait(false);

                _presetMetaCache[preset.Id] = new PresetBackupMeta
                {
                    Id = preset.Id,
                    Timestamp = time,
                    Hash = hash
                };
                SyncMetaToSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError($"SavePreset {preset.Id}", ex);
            }
            finally { _ioLock.Release(); }
        });
    }

    public void DeletePreset(Guid id)
    {
        _debouncer.DebounceAsync($"delete_{id:N}", TimeSpan.FromMilliseconds(100), async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _mainDisk.DeletePreset(id);
                _backupStorage.DeletePreset(id);

                if (_presetMetaCache.TryRemove(id, out _))
                {
                    SyncMetaToSettings();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"DeletePreset {id}", ex);
            }
            finally { _ioLock.Release(); }
        });
    }

    private static GroupRegistry CreateDefaultRegistry() =>
        new() { Groups = [new PresetGroup { Name = Texts.PresetManager_DefaultGroup }] };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debouncer.Dispose();
        _ioLock.Dispose();
    }
}