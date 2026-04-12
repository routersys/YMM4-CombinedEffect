using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Text;

namespace CombinedEffect.Services;

internal sealed class PresetPersistenceService : IPresetPersistenceService
{
    private const string CrcSeparator = "\n<|CRC32|>";
    private static readonly JsonSerializerSettings Settings = new() { Formatting = Formatting.Indented };

    private readonly string _presetsDirectory;

    public PresetPersistenceService()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppDomain.CurrentDomain.BaseDirectory;
        _presetsDirectory = Path.Combine(assemblyDir, "presets");
        Directory.CreateDirectory(_presetsDirectory);
    }

    private string GroupRegistryPath => Path.Combine(_presetsDirectory, "groups.json");

    public GroupRegistry LoadGroupRegistry()
    {
        var content = ReadWithFallback(GroupRegistryPath);
        if (content is null) return CreateDefaultRegistry();
        try
        {
            return JsonConvert.DeserializeObject<GroupRegistry>(content, Settings)
                ?? CreateDefaultRegistry();
        }
        catch { return CreateDefaultRegistry(); }
    }

    public void SaveGroupRegistry(GroupRegistry registry)
    {
        var json = JsonConvert.SerializeObject(registry, Settings);
        WriteWithBackup(GroupRegistryPath, json);
    }

    public EffectPreset? LoadPreset(Guid id)
    {
        var content = ReadWithFallback(GetPresetPath(id));
        if (content is null) return null;
        try
        {
            return JsonConvert.DeserializeObject<EffectPreset>(content, Settings);
        }
        catch { return null; }
    }

    public void SavePreset(EffectPreset preset)
    {
        var json = JsonConvert.SerializeObject(preset, Settings);
        WriteWithBackup(GetPresetPath(preset.Id), json);
    }

    public void DeletePreset(Guid id)
    {
        var path = GetPresetPath(id);
        var bakPath = path + ".bak";
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(bakPath)) File.Delete(bakPath);
    }

    private string GetPresetPath(Guid id) =>
        Path.Combine(_presetsDirectory, $"{id:D}.json");

    private static GroupRegistry CreateDefaultRegistry() =>
        new() { Groups = [new PresetGroup { Name = Texts.PresetManager_DefaultGroup }] };

    private static string? ReadWithFallback(string path) =>
        TryReadVerified(path) ?? TryReadVerified(path + ".bak");

    private static string? TryReadVerified(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var raw = File.ReadAllText(path, Encoding.UTF8);
            var separatorIndex = raw.LastIndexOf(CrcSeparator, StringComparison.Ordinal);
            if (separatorIndex < 0) return raw;

            var content = raw[..separatorIndex];
            var storedHex = raw[(separatorIndex + CrcSeparator.Length)..].Trim();
            if (!uint.TryParse(storedHex, System.Globalization.NumberStyles.HexNumber, null, out var storedCrc))
                return null;

            var contentBytes = Encoding.UTF8.GetBytes(content);
            return Crc32.Compute(contentBytes) == storedCrc ? content : null;
        }
        catch { return null; }
    }

    private static void WriteWithBackup(string path, string content)
    {
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var crc = Crc32.Compute(contentBytes);
        var fullContent = $"{content}{CrcSeparator}{crc:X8}";
        var tempPath = path + ".tmp";
        try
        {
            File.WriteAllText(tempPath, fullContent, Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(tempPath, path, path + ".bak");
            else
                File.Move(tempPath, path);
        }
        catch
        {
            if (File.Exists(tempPath))
                try { File.Delete(tempPath); } catch { }
            throw;
        }
    }
}
