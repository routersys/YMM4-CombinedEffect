using CombinedEffect.Infrastructure;
using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;

namespace CombinedEffect.ViewModels;

internal sealed class PresetItemViewModel : ObservableBase
{
    private readonly IEffectSerializationService _serialization;

    public EffectPreset Model { get; }

    public string Name => Model.Name;
    public bool IsFavorite => Model.IsFavorite;
    public int EffectCount { get; private set; }
    public string EffectInfo { get; private set; } = string.Empty;

    public PresetItemViewModel(EffectPreset model, IEffectSerializationService serialization)
    {
        Model = model;
        _serialization = serialization;
        RefreshEffectInfo(serialization);
    }

    public void RefreshName() => OnPropertyChanged(nameof(Name));

    public void RefreshFavorite() => OnPropertyChanged(nameof(IsFavorite));

    public void RefreshEffectInfo(IEffectSerializationService serialization)
    {
        try
        {
            var effects = serialization.Deserialize(Model.SerializedEffects);
            if (effects is not null)
            {
                EffectCount = effects.Count;
                EffectInfo = string.Join("\n", effects.Select(e => e.Label));
                OnPropertyChanged(nameof(EffectCount));
                OnPropertyChanged(nameof(EffectInfo));
                return;
            }
        }
        catch { }

        EffectCount = 0;
        EffectInfo = string.Empty;
        OnPropertyChanged(nameof(EffectCount));
        OnPropertyChanged(nameof(EffectInfo));
    }
}