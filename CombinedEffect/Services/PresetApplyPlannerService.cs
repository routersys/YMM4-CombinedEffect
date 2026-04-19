using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.Services;

internal sealed class PresetApplyPlannerService(IEffectSerializationService serialization) : IPresetApplyPlannerService
{
    private readonly IEffectSerializationService _serialization = serialization;

    public bool IsSameAsCurrentSelection(EffectPreset preset, ImmutableList<IVideoEffect> currentEffects, string defaultTabName)
    {
        var currentSerialized = _serialization.Serialize(currentEffects);
        var presetState = EffectTabStateService.ResolvePresetState(preset, _serialization, defaultTabName);
        var selectedSerialized = EffectTabStateService.GetSelectedEffectsJson(presetState);
        return string.Equals(currentSerialized, selectedSerialized, StringComparison.Ordinal);
    }

    public PresetApplyPlan CreatePlan(IReadOnlyList<EffectPreset> presets, ImmutableList<IVideoEffect> currentEffects, bool appendCurrentEffects, string defaultTabName)
    {
        if (presets.Count == 0)
            throw new ArgumentException("At least one preset is required.", nameof(presets));

        if (presets.Count == 1 && !appendCurrentEffects)
            return CreateSinglePresetPlan(presets[0], defaultTabName);

        var combinedEffects = new List<IVideoEffect>();
        foreach (var preset in presets)
        {
            var sourceState = EffectTabStateService.ResolvePresetState(preset, _serialization, defaultTabName);
            var selectedEffects = EffectTabStateService.GetSelectedEffects(sourceState, _serialization);
            if (selectedEffects.Count > 0)
                combinedEffects.AddRange(selectedEffects);
        }

        if (appendCurrentEffects && currentEffects.Count > 0)
            combinedEffects.InsertRange(0, currentEffects);

        var immutable = ImmutableList.CreateRange(combinedEffects);
        var state = EffectTabStateService.CreateDefault(immutable, _serialization, defaultTabName);

        return new PresetApplyPlan
        {
            Effects = immutable,
            EffectTabsJson = EffectTabStateService.Serialize(state),
            SelectedPresetJson = presets.Count == 1 ? JsonConvert.SerializeObject(presets[0]) : string.Empty,
        };
    }

    private PresetApplyPlan CreateSinglePresetPlan(EffectPreset preset, string defaultTabName)
    {
        var state = EffectTabStateService.ResolvePresetState(preset, _serialization, defaultTabName);
        var effects = EffectTabStateService.GetSelectedEffects(state, _serialization);
        return new PresetApplyPlan
        {
            Effects = effects,
            EffectTabsJson = EffectTabStateService.Serialize(state),
            SelectedPresetJson = JsonConvert.SerializeObject(preset),
        };
    }
}