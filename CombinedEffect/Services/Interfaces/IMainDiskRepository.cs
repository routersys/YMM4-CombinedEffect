namespace CombinedEffect.Services.Interfaces;

internal interface IMainDiskRepository
{
    (long Timestamp, string Json)? ReadRegistry();
    Task WriteRegistryAsync(long timestamp, string json);
    (long Timestamp, string Json)? ReadPreset(Guid id);
    Task WritePresetAsync(Guid id, long timestamp, string json);
    void DeletePreset(Guid id);
}