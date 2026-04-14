namespace CombinedEffect.Settings;

public class PresetBackupMeta
{
    public Guid Id { get; set; }
    public long Timestamp { get; set; }
    public string Hash { get; set; } = string.Empty;
}