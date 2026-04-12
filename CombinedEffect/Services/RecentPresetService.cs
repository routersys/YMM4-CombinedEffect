using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CombinedEffect.Services;

internal sealed class RecentPresetService : IRecentPresetService
{
    private readonly string _filePath;
    private readonly List<Guid> _recentIds = [];

    public RecentPresetService()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
        _filePath = Path.Combine(assemblyDir, "presets", "RecentPresets.json");
        Load();
    }

    public IEnumerable<Guid> GetRecentIds() => _recentIds.ToList();

    public void Add(Guid id)
    {
        _recentIds.Remove(id);
        _recentIds.Insert(0, id);
        if (_recentIds.Count > 10)
        {
            _recentIds.RemoveRange(10, _recentIds.Count - 10);
        }
        Save();
    }

    public void Remove(Guid id)
    {
        if (_recentIds.Remove(id))
        {
            Save();
        }
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var content = File.ReadAllText(_filePath);
            var ids = JsonConvert.DeserializeObject<List<Guid>>(content);
            if (ids is not null)
            {
                _recentIds.Clear();
                _recentIds.AddRange(ids);
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonConvert.SerializeObject(_recentIds);
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }
}
