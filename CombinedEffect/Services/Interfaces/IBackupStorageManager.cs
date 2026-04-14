namespace CombinedEffect.Services.Interfaces;

internal interface IBackupStorageManager
{
    string? ReadRegistry();
    Task WriteRegistryAsync(string json);
    string? ReadPreset(Guid id);
    Task WritePresetAsync(Guid id, string json);
    void DeletePreset(Guid id);
}