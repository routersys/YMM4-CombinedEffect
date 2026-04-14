using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace CombinedEffect.Services;

internal sealed class PresetExchangeService : IPresetExchangeService
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public void Export(string filePath, IReadOnlyList<EffectPreset> presets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(presets);

        var package = new PresetExchangePackage
        {
            FormatId = PresetExchangeFormat.FormatId,
            Version = PresetExchangeFormat.Version,
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Presets = [.. presets.Select(CloneForExport)]
        };

        var json = JsonConvert.SerializeObject(package, Settings);
        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
            Directory.CreateDirectory(directoryPath);

        File.WriteAllText(filePath, json, new UTF8Encoding(false));
    }

    public IReadOnlyList<EffectPreset> Import(IReadOnlyList<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var imported = new List<EffectPreset>();
        foreach (var filePath in filePaths.Where(static p => !string.IsNullOrWhiteSpace(p)))
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var package = JsonConvert.DeserializeObject<PresetExchangePackage>(json, Settings)
                ?? throw new InvalidDataException(filePath);

            if (package.FormatId != PresetExchangeFormat.FormatId || package.Version != PresetExchangeFormat.Version)
                throw new InvalidDataException(filePath);

            if (package.Presets.Count == 0)
                continue;

            imported.AddRange(package.Presets.Select(CloneForImport));
        }

        return imported;
    }

    private static EffectPreset CloneForExport(EffectPreset source) =>
        new()
        {
            Id = source.Id,
            Name = source.Name,
            IsFavorite = source.IsFavorite,
            SerializedEffects = source.SerializedEffects,
        };

    private static EffectPreset CloneForImport(EffectPreset source) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = source.Name,
            IsFavorite = source.IsFavorite,
            SerializedEffects = source.SerializedEffects,
        };
}