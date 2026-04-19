using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services;
using CombinedEffect.Services.Interfaces;
using System.Collections.Immutable;
using System.Globalization;

namespace CombinedEffect.ViewModels;

internal sealed class PresetItemViewModel : ObservableBase
{
    private readonly IEffectSerializationService _serialization;
    private readonly ILoggerService _logger;

    public EffectPreset Model { get; }

    public string Name => Model.Name;
    public bool IsFavorite => Model.IsFavorite;
    public int EffectCount { get; private set; }
    public string EffectInfo { get; private set; } = string.Empty;

    public PresetItemViewModel(EffectPreset model, IEffectSerializationService serialization, ILoggerService? logger = null)
    {
        Model = model;
        _serialization = serialization;
        _logger = logger ?? ServiceRegistry.Instance.Logger;
        RefreshEffectInfo(serialization);
    }

    public void RefreshName() => OnPropertyChanged(nameof(Name));

    public void RefreshFavorite() => OnPropertyChanged(nameof(IsFavorite));

    public void RefreshEffectInfo(IEffectSerializationService serialization)
    {
        try
        {
            var state = EffectTabStateService.ResolvePresetState(Model, serialization, Texts.EffectTab_FirstName);
            var blocks = new List<string>(state.Tabs.Count);
            var totalCount = 0;
            var maxEffectNameLength = 1;

            foreach (var tab in state.Tabs)
            {
                var effects = serialization.Deserialize(tab.SerializedEffects) ?? ImmutableList<YukkuriMovieMaker.Plugin.Effects.IVideoEffect>.Empty;
                totalCount += effects.Count;

                foreach (var effect in effects)
                {
                    var len = GetTextElementLength(effect.Label);
                    if (len > maxEffectNameLength)
                        maxEffectNameLength = len;
                }

                var lines = new List<string> { $"[{tab.Name}]" };
                if (effects.Count == 0)
                    lines.Add("-");
                else
                    lines.AddRange(effects.Select(e => e.Label));

                blocks.Add(string.Join("\n", lines));
            }

            EffectCount = totalCount;
            var separator = new string('─', Math.Max(1, maxEffectNameLength));
            EffectInfo = string.Join($"\n{separator}\n", blocks);
            OnPropertyChanged(nameof(EffectCount));
            OnPropertyChanged(nameof(EffectInfo));
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to refresh preset effect info.", ex);
        }

        EffectCount = 0;
        EffectInfo = string.Empty;
        OnPropertyChanged(nameof(EffectCount));
        OnPropertyChanged(nameof(EffectInfo));
    }

    private static int GetTextElementLength(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return StringInfo.ParseCombiningCharacters(text).Length;
    }
}