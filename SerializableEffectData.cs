using System.Text.Json.Serialization;

namespace CombinedEffect.Models
{
    public class SerializableEffectData
    {
        public string? TypeName { get; set; }
        public Dictionary<string, object?>? Properties { get; set; }
    }
}