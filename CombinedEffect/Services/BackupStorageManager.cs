using CombinedEffect.Infrastructure;
using CombinedEffect.Services.Interfaces;
using System.IO;

namespace CombinedEffect.Services;

internal sealed class BackupStorageManager : IBackupStorageManager
{
    private readonly string _backupDirectory;

    public BackupStorageManager()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _backupDirectory = Path.Combine(baseDir, "user", "backup", "CombinedEffect");
        Directory.CreateDirectory(_backupDirectory);
    }

    private string GetPresetPath(Guid id) => Path.Combine(_backupDirectory, $"{id:D}.json");
    private string GetRegistryPath() => Path.Combine(_backupDirectory, "registry.json");

    public string? ReadRegistry() => AtomicFileWriter.ReadVerified(GetRegistryPath());

    public Task WriteRegistryAsync(string json) => AtomicFileWriter.WriteAtomicAsync(GetRegistryPath(), json);

    public string? ReadPreset(Guid id) => AtomicFileWriter.ReadVerified(GetPresetPath(id));

    public Task WritePresetAsync(Guid id, string json) => AtomicFileWriter.WriteAtomicAsync(GetPresetPath(id), json);

    public void DeletePreset(Guid id)
    {
        var path = GetPresetPath(id);
        var bakPath = path + ".bak";
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(bakPath)) File.Delete(bakPath);
    }
}