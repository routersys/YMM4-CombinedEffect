using System.Collections.ObjectModel;
using CombinedEffect.Helpers;
using System.Text.Json.Serialization;

namespace CombinedEffect.Models
{
    public class PresetGroup : ObservableObject
    {
        private string _name = "新規グループ";
        private bool _isEditing;

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public ObservableCollection<EffectPreset> Presets { get; set; } = new();

        [JsonIgnore]
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }
    }
}