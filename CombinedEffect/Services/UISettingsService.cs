using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;

namespace CombinedEffect.Services;

internal sealed class UISettingsService : IUISettingsService
{
    private readonly string _filePath;

    public UISettings Settings { get; }

    public UISettingsService()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
        _filePath = Path.Combine(assemblyDir, "presets", "UISettings.json");
        Settings = Load();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonConvert.SerializeObject(Settings);
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }

    private UISettings Load()
    {
        if (!File.Exists(_filePath)) return new UISettings();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<UISettings>(json) ?? new UISettings();
        }
        catch
        {
            return new UISettings();
        }
    }
}
