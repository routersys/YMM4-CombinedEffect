using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;
using CombinedEffect.Helpers;
using System.Text.Json.Serialization;

namespace CombinedEffect.Models
{
    public class EffectPreset : ObservableObject
    {
        private string _name = "新規プリセット";
        private bool _isFavorite;
        private bool _isEditing;

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set => SetProperty(ref _isFavorite, value);
        }

        public string SerializedEffects { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }
    }
}