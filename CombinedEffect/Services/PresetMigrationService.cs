using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace CombinedEffect.Services;

internal sealed class PresetMigrationService : IPresetMigrationService
{
    private static readonly string LegacyFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YukkuriMovieMaker4", "user", "plugins", "CombinedEffect", "presets.json");

    private readonly IPresetPersistenceService _persistence;

    public PresetMigrationService(IPresetPersistenceService persistence)
    {
        _persistence = persistence;
    }

    public void MigrateIfRequired()
    {
        if (!File.Exists(LegacyFilePath)) return;
        try
        {
            var json = File.ReadAllText(LegacyFilePath, Encoding.UTF8);
            var legacyGroups = JsonConvert.DeserializeObject<List<LegacyPresetGroup>>(json);
            if (legacyGroups is null) return;

            var virtualNames = new HashSet<string>
            {
                Texts.PresetManager_GroupAll,
                Texts.PresetManager_GroupFavorites
            };

            var registry = new GroupRegistry();
            foreach (var legacyGroup in legacyGroups.Where(g => !virtualNames.Contains(g.Name ?? string.Empty)))
            {
                var group = new PresetGroup { Name = legacyGroup.Name ?? Texts.PresetManager_DefaultGroup };
                foreach (var legacyPreset in legacyGroup.Presets ?? [])
                {
                    var preset = new EffectPreset
                    {
                        Name = legacyPreset.Name ?? Texts.PresetManager_NewPreset,
                        IsFavorite = legacyPreset.IsFavorite,
                        SerializedEffects = legacyPreset.SerializedEffects ?? string.Empty,
                    };
                    _persistence.SavePreset(preset);
                    group.PresetIds.Add(preset.Id);
                }
                registry.Groups.Add(group);
            }

            _persistence.SaveGroupRegistry(registry);
            File.Move(LegacyFilePath, LegacyFilePath + ".migrated", overwrite: true);
        }
        catch { }
    }

    private sealed class LegacyPresetGroup
    {
        public string? Name { get; set; }
        public List<LegacyEffectPreset>? Presets { get; set; }
    }

    private sealed class LegacyEffectPreset
    {
        public string? Name { get; set; }
        public bool IsFavorite { get; set; }
        public string? SerializedEffects { get; set; }
    }
}
