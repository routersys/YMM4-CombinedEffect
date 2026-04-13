using CombinedEffect.Models;

namespace CombinedEffect.Services.Interfaces;

internal interface IPresetPersistenceService
{
    Task<GroupRegistry> LoadGroupRegistryAsync();
    void SaveGroupRegistry(GroupRegistry registry);
    Task<EffectPreset?> LoadPresetAsync(Guid id);
    void SavePreset(EffectPreset preset);
    void DeletePreset(Guid id);
}