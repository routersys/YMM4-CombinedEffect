using CombinedEffect.Attributes;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using Newtonsoft.Json;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.Effect;

[PluginDetails(AuthorName = "routersys")]

[VideoEffect(nameof(Texts.VideoEffect_Name), [nameof(Texts.VideoEffect_Category_Tool)], [nameof(Texts.VideoEffect_Tag_Combined), nameof(Texts.VideoEffect_Tag_Preset), nameof(Texts.VideoEffect_Tag_Group)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class CombinedEffect : VideoEffectBase
{
    private string PresetName
    {
        get;
        set;
    } = string.Empty;

    public override string Label
    {
        get
        {
            var count = Effects.Count;
            return string.IsNullOrEmpty(PresetName)
                ? string.Format(Texts.CombinedEffect_ActiveEffectsCount, Texts.CombinedEffect_DisplayName, count)
                : string.Format(Texts.CombinedEffect_ActiveEffectsCountWithPreset, Texts.CombinedEffect_DisplayName, count, PresetName);
        }
    }

    [Display(GroupName = nameof(Texts.CombinedEffect_PresetGroup), Name = nameof(Texts.CombinedEffect_EmptyLabel), ResourceType = typeof(Texts))]
    [PresetManagerControl]
    [JsonIgnore]
    public bool PresetManagerVisible { get; set; } = true;

    [Display(GroupName = nameof(Texts.CombinedEffect_EffectGroup), Name = nameof(Texts.CombinedEffect_EmptyLabel), ResourceType = typeof(Texts))]
    [EffectTabManagerControl]
    [JsonIgnore]
    public bool EffectTabManagerVisible { get; set; } = true;

    [Display(GroupName = nameof(Texts.CombinedEffect_EffectGroup), Name = nameof(Texts.CombinedEffect_EmptyLabel), ResourceType = typeof(Texts))]
    [VideoEffectSelector(PropertyEditorSize = PropertyEditorSize.FullWidth)]
    public ImmutableList<IVideoEffect> Effects
    {
        get;
        set
        {
            if (!Set(ref field, value)) return;
            OnPropertyChanged(nameof(Label));
        }
    } = ImmutableList<IVideoEffect>.Empty;

    public string? SelectedPresetJson
    {
        get;
        set
        {
            if (!Set(ref field, value)) return;
            if (string.IsNullOrEmpty(value))
            {
                PresetName = string.Empty;
                OnPropertyChanged(nameof(Label));
                return;
            }
            try
            {
                var preset = JsonConvert.DeserializeObject<EffectPreset>(value);
                PresetName = preset?.Name ?? string.Empty;
            }
            catch
            {
                PresetName = string.Empty;
            }
            OnPropertyChanged(nameof(Label));
        }
    }

    public string? EffectTabsJson
    {
        get;
        set => Set(ref field, value);
    }

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) =>
        new CombinedEffectProcessor(this, devices);

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) =>
        [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => Effects;
}