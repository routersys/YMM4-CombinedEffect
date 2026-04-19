using CombinedEffect.Models;
using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.Services.Interfaces;

internal interface IPresetApplyPlannerService
{
    bool IsSameAsCurrentSelection(EffectPreset preset, ImmutableList<IVideoEffect> currentEffects, string defaultTabName);
    PresetApplyPlan CreatePlan(IReadOnlyList<EffectPreset> presets, ImmutableList<IVideoEffect> currentEffects, bool appendCurrentEffects, string defaultTabName);
}