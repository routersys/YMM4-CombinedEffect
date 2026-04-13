using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.Services;

internal sealed class EffectSerializationService : IEffectSerializationService
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public string Serialize(ImmutableList<IVideoEffect> effects) =>
        JsonConvert.SerializeObject(effects, Settings);

    public ImmutableList<IVideoEffect>? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var list = JsonConvert.DeserializeObject<List<IVideoEffect>>(json, Settings);
            return list?.ToImmutableList();
        }
        catch (JsonSerializationException) { return null; }
        catch (JsonReaderException) { return null; }
    }
}