using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Windows;

namespace CombinedEffect.Services;

internal sealed class PresetPersistenceService : IPresetPersistenceService, IDisposable
{
    private static readonly JsonSerializerSettings Settings = new() { Formatting = Formatting.Indented };

    private readonly string _presetsDirectory;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly AsyncDebouncer _debouncer = new();
    private bool _disposed;

    public PresetPersistenceService()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppDomain.CurrentDomain.BaseDirectory;
        _presetsDirectory = Path.Combine(assemblyDir, Constants.DirectoryPresets);
        Directory.CreateDirectory(_presetsDirectory);
    }

    private string GroupRegistryPath => Path.Combine(_presetsDirectory, Constants.FileRegistry);

    public async Task<GroupRegistry> LoadGroupRegistryAsync()
    {
        return await Task.Run(async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var content = AtomicFileWriter.ReadVerified(GroupRegistryPath);
                if (content is null) return CreateDefaultRegistry();
                return JsonConvert.DeserializeObject<GroupRegistry>(content, Settings)
                    ?? CreateDefaultRegistry();
            }
            catch { return CreateDefaultRegistry(); }
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
                await AtomicFileWriter.WriteAtomicAsync(GroupRegistryPath, json).ConfigureAwait(false);
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

    public async Task<EffectPreset?> LoadPresetAsync(Guid id)
    {
        return await Task.Run(async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var content = AtomicFileWriter.ReadVerified(GetPresetPath(id));
                if (content is null) return null;
                return JsonConvert.DeserializeObject<EffectPreset>(content, Settings);
            }
            catch { return null; }
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
                await AtomicFileWriter.WriteAtomicAsync(GetPresetPath(preset.Id), json).ConfigureAwait(false);
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

    public void DeletePreset(Guid id)
    {
        _debouncer.DebounceAsync($"delete_{id:N}", TimeSpan.FromMilliseconds(100), async () =>
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var path = GetPresetPath(id);
                var bakPath = path + ".bak";
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(bakPath)) File.Delete(bakPath);
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

    private string GetPresetPath(Guid id) =>
        Path.Combine(_presetsDirectory, $"{id:D}.json");

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