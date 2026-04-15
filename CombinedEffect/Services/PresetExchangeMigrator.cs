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

        return package;
    }
}