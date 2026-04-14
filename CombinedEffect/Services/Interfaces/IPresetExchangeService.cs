using CombinedEffect.Models;

namespace CombinedEffect.Services.Interfaces;

internal interface IPresetExchangeService
{
    void Export(string filePath, IReadOnlyList<EffectPreset> presets);
    IReadOnlyList<EffectPreset> Import(IReadOnlyList<string> filePaths);
}