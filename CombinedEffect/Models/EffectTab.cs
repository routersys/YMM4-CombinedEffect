namespace CombinedEffect.Models;

public sealed class EffectTab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SerializedEffects { get; set; } = string.Empty;
}