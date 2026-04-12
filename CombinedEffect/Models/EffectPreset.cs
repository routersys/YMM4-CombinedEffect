namespace CombinedEffect.Models;

public sealed class EffectPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public string SerializedEffects { get; set; } = string.Empty;
}