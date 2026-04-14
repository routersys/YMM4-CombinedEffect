using CombinedEffect.Localization;
using CombinedEffect.Models;
using System.IO;

namespace CombinedEffect.Services;

internal static class PresetExchangeMigrator
{
    public static PresetExchangePackage Migrate(PresetExchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (package.Version > PresetExchangeFormat.Version)
            throw new InvalidDataException(
                string.Format(Texts.PresetExchangeMigrator_UnsupportedVersion, package.Version, PresetExchangeFormat.Version));

        var current = package;

        if (current.Version == 1 && PresetExchangeFormat.Version >= 2)
            current = MigrateFromV1ToV2(current);

        return current;
    }

    private static PresetExchangePackage MigrateFromV1ToV2(PresetExchangePackage source) =>
        new()
        {
            FormatId = source.FormatId,
            Version = 2,
            ExportedAtUtc = source.ExportedAtUtc,
            Presets = [.. source.Presets.Select(static p => new EffectPreset
            {
                Id = p.Id,
                Name = p.Name,
                IsFavorite = p.IsFavorite,
                SerializedEffects = p.SerializedEffects,
            })]
        };
}