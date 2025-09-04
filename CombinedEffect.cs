using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using CombinedEffect.Views;
using System.Text.Json;
using System.Text.Json.Serialization;
using CombinedEffect.Models;
using CombinedEffect.ViewModels;
using YukkuriMovieMaker.Controls;
using CombinedEffect.Helpers;

namespace CombinedEffect
{
    [VideoEffect("まとめてエフェクト", ["ツール"], ["まとめて", "プリセット", "グループ"], isAviUtlSupported: false)]
    public class CombinedEffect : VideoEffectBase
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            TypeInfoResolver = new PolymorphicTypeResolver()
        };

        public override string Label => string.IsNullOrEmpty(presetName) ? "まとめてエフェクト" : $"まとめてエフェクト [{presetName}]";
        private string presetName = string.Empty;

        [Display(GroupName = "プリセット管理", Name = "")]
        [PresetManagerControl]
        [JsonIgnore]
        public bool PresetManager { get; set; } = true;

        [Display(GroupName = "適用中エフェクト", Name = "")]
        [VideoEffectSelector(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public ImmutableList<IVideoEffect> Effects { get => effects; set => Set(ref effects, value); }
        private ImmutableList<IVideoEffect> effects = ImmutableList<IVideoEffect>.Empty;

        public string? SelectedPresetJson
        {
            get => selectedPresetJson;
            set
            {
                if (Set(ref selectedPresetJson, value))
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            var preset = JsonSerializer.Deserialize<EffectPreset>(value, _jsonOptions);
                            if (preset != null)
                            {
                                presetName = preset.Name;
                                if (!string.IsNullOrEmpty(preset.SerializedEffects))
                                {
                                    var deserializedEffects = PresetManagerViewModel.DeserializeEffects(preset.SerializedEffects, _jsonOptions);
                                    if (deserializedEffects != null)
                                    {
                                        Effects = deserializedEffects;
                                    }
                                }
                            }
                            else
                            {
                                presetName = string.Empty;
                            }
                        }
                        catch
                        {
                            presetName = string.Empty;
                        }
                    }
                    else
                    {
                        presetName = string.Empty;
                    }
                    OnPropertyChanged(nameof(Label));
                }
            }
        }
        private string? selectedPresetJson;


        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new CombinedEffectProcessor(this, devices);
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return Enumerable.Empty<string>();
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => effects;
    }
}