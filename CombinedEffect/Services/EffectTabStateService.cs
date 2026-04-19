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