namespace CombinedEffect.Models;

public sealed class EffectTabState
{
    public Guid? SelectedTabId { get; set; }
    public List<EffectTab> Tabs { get; set; } = [];
}