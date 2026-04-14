using CombinedEffect.Infrastructure;
using CombinedEffect.Services.Interfaces;
using System.IO;
using System.Reflection;

namespace CombinedEffect.Services;

internal sealed class MainDiskRepository : IMainDiskRepository
{
    private readonly string _presetsDirectory;
    private readonly string _registryPath;

    public MainDiskRepository()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppDomain.CurrentDomain.BaseDirectory;
        _presetsDirectory = Path.Combine(assemblyDir, Constants.DirectoryPresets);
        Directory.CreateDirectory(_presetsDirectory);
        _registryPath = Path.Combine(_presetsDirectory, Constants.FileRegistry);
    }

    private string GetPresetPath(Guid id) => Path.Combine(_presetsDirectory, $"{id:D}.json");

    public (long Timestamp, string Json)? ReadRegistry()
    {
        var json = AtomicFileWriter.ReadVerified(_registryPath);
        if (json == null) return null;
        var time = File.Exists(_registryPath) ? File.GetLastWriteTimeUtc(_registryPath).Ticks : 0;
        return (time, json);
    }

    public async Task WriteRegistryAsync(long timestamp, string json)
    {
        await AtomicFileWriter.WriteAtomicAsync(_registryPath, json).ConfigureAwait(false);
        File.SetLastWriteTimeUtc(_registryPath, new DateTime(timestamp, DateTimeKind.Utc));
    }

    public (long Timestamp, string Json)? ReadPreset(Guid id)
    {
        var path = GetPresetPath(id);
        var json = AtomicFileWriter.ReadVerified(path);
        if (json == null) return null;
        var time = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
        return (time, json);
    }

    public async Task WritePresetAsync(Guid id, long timestamp, string json)
    {
        var path = GetPresetPath(id);
        await AtomicFileWriter.WriteAtomicAsync(path, json).ConfigureAwait(false);
        File.SetLastWriteTimeUtc(path, new DateTime(timestamp, DateTimeKind.Utc));
    }

    public void DeletePreset(Guid id)
    {
        var path = GetPresetPath(id);
        var bakPath = path + ".bak";
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(bakPath)) File.Delete(bakPath);
    }
}