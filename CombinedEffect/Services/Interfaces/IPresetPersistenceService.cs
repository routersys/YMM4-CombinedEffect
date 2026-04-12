using CombinedEffect.Models;

namespace CombinedEffect.Services.Interfaces;

internal interface IPresetPersistenceService
{
    GroupRegistry LoadGroupRegistry();
    void SaveGroupRegistry(GroupRegistry registry);
    EffectPreset? LoadPreset(Guid id);
    void SavePreset(EffectPreset preset);
    void DeletePreset(Guid id);
}
