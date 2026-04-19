using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.Models;

internal sealed class PresetApplyPlan
{
    public required ImmutableList<IVideoEffect> Effects { get; init; }
    public required string EffectTabsJson { get; init; }
    public required string SelectedPresetJson { get; init; }
}