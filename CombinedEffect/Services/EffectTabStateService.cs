using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.Services;

internal static class EffectTabStateService
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static string Serialize(EffectTabState state) =>
        JsonConvert.SerializeObject(state, Settings);

    public static bool TryDeserialize(string? json, out EffectTabState state)
    {
        state = new EffectTabState();
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var parsed = JsonConvert.DeserializeObject<EffectTabState>(json, Settings);
            if (parsed is null)
                return false;
            state = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static EffectTabState CreateDefault(
        ImmutableList<IVideoEffect> effects,
        IEffectSerializationService serialization,
        string defaultTabName)
    {
        var serialized = serialization.Serialize(effects);
        return CreateSingleTabState(serialized, defaultTabName);
    }

    public static EffectTabState CreateSingleTabState(string serializedEffects, string defaultTabName)
    {
        var tab = new EffectTab
        {
            Name = string.IsNullOrWhiteSpace(defaultTabName) ? "Tab" : defaultTabName,
            SerializedEffects = serializedEffects ?? string.Empty,
        };

        return new EffectTabState
        {
            SelectedTabId = tab.Id,
            Tabs = [tab],
        };
    }

    public static EffectTabState ResolveEffectState(
        string? serializedTabs,
        ImmutableList<IVideoEffect> fallbackEffects,
        IEffectSerializationService serialization,
        string defaultTabName) =>
        ResolveState(serializedTabs, null, Guid.Empty, fallbackEffects, serialization, defaultTabName);

    public static EffectTabState ResolvePresetState(
        EffectPreset preset,
        IEffectSerializationService serialization,
        string defaultTabName) =>
        ResolveState(preset.SerializedTabs, preset.SerializedEffects, preset.Id, ImmutableList<IVideoEffect>.Empty, serialization, defaultTabName);

    public static EffectTabState Normalize(
        EffectTabState? state,
        ImmutableList<IVideoEffect> fallbackEffects,
        IEffectSerializationService serialization,
        string defaultTabName)
    {
        var normalized = state ?? CreateDefault(fallbackEffects, serialization, defaultTabName);

        normalized.Tabs ??= [];
        if (normalized.Tabs.Count == 0)
            normalized.Tabs.Add(new EffectTab
            {
                Name = string.IsNullOrWhiteSpace(defaultTabName) ? "Tab" : defaultTabName,
                SerializedEffects = serialization.Serialize(fallbackEffects),
            });

        for (var i = 0; i < normalized.Tabs.Count; i++)
        {
            var tab = normalized.Tabs[i];
            if (tab.Id == Guid.Empty)
                tab.Id = Guid.NewGuid();
            if (string.IsNullOrWhiteSpace(tab.Name))
                tab.Name = i == 0 ? defaultTabName : $"{defaultTabName} {i + 1}";
            tab.SerializedEffects ??= string.Empty;
        }

        var selected = normalized.SelectedTabId;
        if (selected is null || normalized.Tabs.All(t => t.Id != selected.Value))
            normalized.SelectedTabId = normalized.Tabs[0].Id;

        return normalized;
    }

    private static EffectTabState ResolveState(
        string? serializedTabs,
        string? serializedEffects,
        Guid fallbackLegacyTabId,
        ImmutableList<IVideoEffect> fallbackEffects,
        IEffectSerializationService serialization,
        string defaultTabName)
    {
        EffectTabState? parsed = null;
        if (TryDeserialize(serializedTabs, out var state))
            parsed = state;
        else if (!string.IsNullOrWhiteSpace(serializedEffects))
        {
            parsed = CreateSingleTabState(serializedEffects, defaultTabName);
            var tab = parsed.Tabs[0];
            tab.Id = fallbackLegacyTabId == Guid.Empty ? tab.Id : fallbackLegacyTabId;
            parsed.SelectedTabId = tab.Id;
        }

        return Normalize(parsed, fallbackEffects, serialization, defaultTabName);
    }

    public static EffectTab GetSelectedTab(EffectTabState state)
    {
        var selectedId = state.SelectedTabId;
        if (selectedId is not null)
        {
            var selected = state.Tabs.FirstOrDefault(t => t.Id == selectedId.Value);
            if (selected is not null)
                return selected;
        }

        return state.Tabs[0];
    }

    public static ImmutableList<IVideoEffect> GetSelectedEffects(EffectTabState state, IEffectSerializationService serialization)
    {
        var selected = GetSelectedTab(state);
        return serialization.Deserialize(selected.SerializedEffects) ?? ImmutableList<IVideoEffect>.Empty;
    }

    public static string GetSelectedEffectsJson(EffectTabState state)
    {
        var selected = GetSelectedTab(state);
        return selected.SerializedEffects ?? string.Empty;
    }
}