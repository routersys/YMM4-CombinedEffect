namespace CombinedEffect.Models;

internal sealed class PresetExchangePackage
{
    public string FormatId { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset ExportedAtUtc { get; set; }
    public List<EffectPreset> Presets { get; set; } = [];
}