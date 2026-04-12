using CombinedEffect.Attributes;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services;
using Newtonsoft.Json;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.Effect;

[VideoEffect(nameof(Texts.VideoEffect_Name), [nameof(Texts.VideoEffect_Category_Tool)], [nameof(Texts.VideoEffect_Tag_Combined), nameof(Texts.VideoEffect_Tag_Preset), nameof(Texts.VideoEffect_Tag_Group)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class CombinedEffect : VideoEffectBase
{
    private string _presetName = string.Empty;
    private string? _selectedPresetJson;
    private ImmutableList<IVideoEffect> _effects = ImmutableList<IVideoEffect>.Empty;

    public override string Label
    {
        get
        {
            var count = Effects.Count;
            return string.IsNullOrEmpty(_presetName)
                ? $"{Texts.CombinedEffect_DisplayName} 適用中: {count}個"
                : $"{Texts.CombinedEffect_DisplayName} 適用中: {count}個 [{_presetName}]";
        }
    }

    [Display(GroupName = nameof(Texts.CombinedEffect_PresetGroup), Name = nameof(Texts.CombinedEffect_EmptyLabel), ResourceType = typeof(Texts))]
    [PresetManagerControl]
    [JsonIgnore]
    public bool PresetManagerVisible { get; set; } = true;

    [Display(GroupName = nameof(Texts.CombinedEffect_EffectGroup), Name = nameof(Texts.CombinedEffect_EmptyLabel), ResourceType = typeof(Texts))]
    [VideoEffectSelector(PropertyEditorSize = PropertyEditorSize.FullWidth)]
    public ImmutableList<IVideoEffect> Effects
    {
        get => _effects;
        set
        {
            if (!Set(ref _effects, value)) return;
            OnPropertyChanged(nameof(Label));
        }
    }

    public string? SelectedPresetJson
    {
        get => _selectedPresetJson;
        set
        {
            if (!Set(ref _selectedPresetJson, value)) return;
            if (string.IsNullOrEmpty(value))
            {
                _presetName = string.Empty;
                OnPropertyChanged(nameof(Label));
                return;
            }
            try
            {
                var preset = JsonConvert.DeserializeObject<EffectPreset>(value);
                if (preset is null)
                {
                    _presetName = string.Empty;
                }
                else
                {
                    _presetName = preset.Name;
                    var deserialized = ServiceRegistry.Instance.EffectSerialization.Deserialize(preset.SerializedEffects);
                    if (deserialized is not null) Effects = deserialized;
                }
            }
            catch
            {
                _presetName = string.Empty;
            }
            OnPropertyChanged(nameof(Label));
        }
    }

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) =>
        new CombinedEffectProcessor(this, devices);

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) =>
        [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => _effects;
}
