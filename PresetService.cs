using CombinedEffect.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace CombinedEffect.Services
{
    public class PresetService
    {
        private readonly string _filePath;

        public PresetService()
        {
            var userFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YukkuriMovieMaker4", "user");
            var pluginDataPath = Path.Combine(userFolderPath, "plugins", "CombinedEffect");
            Directory.CreateDirectory(pluginDataPath);
            _filePath = Path.Combine(pluginDataPath, "presets.json");
        }

        public ObservableCollection<PresetGroup> Load()
        {
            if (!File.Exists(_filePath))
            {
                var defaultGroup = new PresetGroup { Name = "デフォルト" };
                return new ObservableCollection<PresetGroup> { defaultGroup };
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var options = new JsonSerializerOptions { WriteIndented = true };
                return JsonSerializer.Deserialize<ObservableCollection<PresetGroup>>(json, options) ?? new ObservableCollection<PresetGroup>();
            }
            catch
            {
                var defaultGroup = new PresetGroup { Name = "デフォルト" };
                return new ObservableCollection<PresetGroup> { defaultGroup };
            }
        }

        public void Save(ObservableCollection<PresetGroup> groups)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(groups, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プリセットの保存に失敗しました。\n{ex.Message}", "保存エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}