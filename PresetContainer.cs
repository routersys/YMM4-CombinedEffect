using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;
using System.Text.Json.Serialization;

namespace CombinedEffect.Models
{
    public class PresetContainer
    {
        [JsonInclude]
        public ImmutableList<IVideoEffect> Effects { get; set; } = ImmutableList<IVideoEffect>.Empty;
    }
}