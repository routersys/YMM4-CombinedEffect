using CombinedEffect.Localization;
using YukkuriMovieMaker.Plugin;

namespace CombinedEffect.Settings;

public class CombinedEffectSettings : SettingsBase<CombinedEffectSettings>
{
    public override string Name => Texts.VideoEffect_Name;
    public override SettingsCategory Category => SettingsCategory.VideoEffect;
    public override bool HasSettingView => false;
    public override object SettingView => null!;
    public static CombinedEffectSettings Instance => Default;

    public long RegistryBackupTimestamp { get; set; }
    public string RegistryBackupHash { get; set; } = string.Empty;
    public PresetBackupMeta[] Presets { get; set; } = [];

    public override void Initialize()
    {

    }
}