using System.Collections.Generic;

namespace CombinedEffect.Models
{
    public class SerializableEffect
    {
        public string? TypeName { get; set; }
        public Dictionary<string, object?>? Properties { get; set; }
    }
}