using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.Services.Interfaces;

internal interface IEffectSerializationService
{
    string Serialize(ImmutableList<IVideoEffect> effects);
    ImmutableList<IVideoEffect>? Deserialize(string? json);
}
