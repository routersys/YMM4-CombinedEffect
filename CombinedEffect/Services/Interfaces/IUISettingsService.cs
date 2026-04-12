using CombinedEffect.Models;

namespace CombinedEffect.Services.Interfaces;

internal interface IUISettingsService
{
    UISettings Settings { get; }
    void Save();
}
