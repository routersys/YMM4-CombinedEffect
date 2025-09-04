using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.Helpers
{
    public class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

            if (jsonTypeInfo.Type == typeof(IVideoEffect))
            {
                jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$type",
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor
                };

                var derivedTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(asm => { try { return asm.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .Where(t => typeof(IVideoEffect).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var derivedType in derivedTypes)
                {
                    jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(
                        new JsonDerivedType(derivedType, derivedType.FullName!));
                }
            }

            if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object)
            {
                foreach (var prop in jsonTypeInfo.Properties)
                {
                    if (prop.Get is not null && prop.Set is null)
                    {
                        if (prop.PropertyType.GetMethod("CopyFrom") != null)
                        {
                            prop.Set = (obj, value) => {
                                var target = prop.Get(obj);
                                if (target != null && value != null)
                                {
                                    target.GetType().GetMethod("CopyFrom")?.Invoke(target, new[] { value });
                                }
                            };
                        }
                    }

                    var originalShouldSerialize = prop.ShouldSerialize;
                    prop.ShouldSerialize = (obj, value) =>
                    {
                        if (prop.PropertyType == typeof(Type))
                        {
                            return false;
                        }

                        var propValue = prop.Get?.Invoke(obj);
                        if (propValue is Type)
                        {
                            return false;
                        }

                        return originalShouldSerialize?.Invoke(obj, value) ?? true;
                    };
                }
            }

            return jsonTypeInfo;
        }
    }
}