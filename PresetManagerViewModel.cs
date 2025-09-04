using CombinedEffect.Helpers;
using CombinedEffect.Models;
using CombinedEffect.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.ViewModels
{
    public class PresetManagerViewModel : ObservableObject
    {
        private readonly PresetService _presetService;
        private readonly ItemProperty[] _itemProperties;
        private readonly CombinedEffect _combinedEffect;
        private readonly JsonSerializerOptions _jsonOptions;

        public ObservableCollection<PresetGroup> Groups { get; }
        public ObservableCollection<EffectPreset> DisplayedPresets { get; } = new();

        private PresetGroup? _selectedGroup;
        public PresetGroup? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value) && value != null)
                {
                    FinishEditing();
                    FinishEditingGroup();
                    UpdateDisplayedPresets();
                }
            }
        }

        private EffectPreset? _selectedPreset;
        public EffectPreset? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetProperty(ref _selectedPreset, value))
                {
                    FinishEditing();
                }
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    OnPropertyChanged(nameof(IsSearchTextEmpty));
                    UpdateDisplayedPresets();
                }
            }
        }
        public bool IsSearchTextEmpty => string.IsNullOrEmpty(SearchText);


        public ICommand AddGroupCommand { get; }
        public ICommand RemoveGroupCommand { get; }
        public ICommand AddPresetCommand { get; }
        public ICommand RemovePresetCommand { get; }
        public ICommand ApplyPresetCommand { get; }
        public ICommand UpdatePresetCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand StartEditingCommand { get; }
        public ICommand FinishEditingCommand { get; }
        public ICommand StartEditingGroupCommand { get; }
        public ICommand FinishEditingGroupCommand { get; }

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public PresetManagerViewModel(ItemProperty[] itemProperties)
        {
            _itemProperties = itemProperties;
            _combinedEffect = (CombinedEffect)itemProperties[0].PropertyOwner;
            _presetService = new PresetService();
            Groups = _presetService.Load();

            _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

            AddGroupCommand = new RelayCommand<object>(_ => AddGroup());
            RemoveGroupCommand = new RelayCommand<object>(_ => RemoveGroup(), _ => SelectedGroup != null && SelectedGroup.Name != "すべて" && SelectedGroup.Name != "お気に入り" && SelectedGroup.Name != "デフォルト");
            AddPresetCommand = new RelayCommand<object>(_ => AddPreset(), _ => SelectedGroup != null && SelectedGroup.Name != "すべて" && SelectedGroup.Name != "お気に入り");
            RemovePresetCommand = new RelayCommand<object>(_ => RemovePreset(), _ => SelectedPreset != null);
            ApplyPresetCommand = new RelayCommand<object>(_ => ApplyPreset(), _ => SelectedPreset != null);
            UpdatePresetCommand = new RelayCommand<object>(_ => UpdatePreset(), _ => SelectedPreset != null);
            ToggleFavoriteCommand = new RelayCommand<EffectPreset>(ToggleFavorite);
            StartEditingCommand = new RelayCommand<EffectPreset>(StartEditing);
            FinishEditingCommand = new RelayCommand<EffectPreset>(p => FinishEditing(p));
            StartEditingGroupCommand = new RelayCommand<PresetGroup>(StartEditingGroup);
            FinishEditingGroupCommand = new RelayCommand<PresetGroup>(p => FinishEditingGroup(p));

            InitializeGroups();
            SelectedGroup = Groups.FirstOrDefault(g => g.Name == "すべて");
        }

        private void StartEditing(EffectPreset? preset)
        {
            if (preset != null)
            {
                FinishEditing();
                preset.IsEditing = true;
            }
        }

        private void FinishEditing(EffectPreset? preset = null)
        {
            var editingPreset = preset ?? DisplayedPresets.FirstOrDefault(p => p.IsEditing);
            if (editingPreset != null)
            {
                editingPreset.IsEditing = false;
                SaveChanges();
            }
        }

        private void StartEditingGroup(PresetGroup? group)
        {
            if (group != null && group.Name != "すべて" && group.Name != "お気に入り" && group.Name != "デフォルト")
            {
                FinishEditingGroup();
                group.IsEditing = true;
            }
        }

        private void FinishEditingGroup(PresetGroup? group = null)
        {
            var editingGroup = group ?? Groups.FirstOrDefault(g => g.IsEditing);
            if (editingGroup != null)
            {
                editingGroup.IsEditing = false;
                SaveChanges();
            }
        }


        private void InitializeGroups()
        {
            if (!Groups.Any(g => g.Name == "すべて"))
                Groups.Insert(0, new PresetGroup { Name = "すべて" });
            if (!Groups.Any(g => g.Name == "お気に入り"))
                Groups.Insert(1, new PresetGroup { Name = "お気に入り" });
        }

        private void UpdateDisplayedPresets()
        {
            DisplayedPresets.Clear();
            if (SelectedGroup == null) return;

            IEnumerable<EffectPreset> presets;
            if (SelectedGroup.Name == "すべて")
            {
                presets = Groups.Where(g => g.Name != "すべて" && g.Name != "お気に入り").SelectMany(g => g.Presets);
            }
            else if (SelectedGroup.Name == "お気に入り")
            {
                presets = Groups.SelectMany(g => g.Presets).Where(p => p.IsFavorite);
            }
            else
            {
                presets = SelectedGroup.Presets;
            }

            if (!IsSearchTextEmpty)
            {
                presets = presets.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var preset in presets.OrderBy(p => p.Name))
            {
                DisplayedPresets.Add(preset);
            }
        }

        private void AddGroup()
        {
            var newGroup = new PresetGroup { Name = "新規グループ" };
            Groups.Add(newGroup);
            SelectedGroup = newGroup;
            StartEditingGroup(newGroup);
        }

        private void RemoveGroup()
        {
            if (SelectedGroup != null && SelectedGroup.Name != "デフォルト" && SelectedGroup.Name != "すべて" && SelectedGroup.Name != "お気に入り")
            {
                Groups.Remove(SelectedGroup);
                SelectedGroup = Groups.FirstOrDefault(g => g.Name == "すべて");
                SaveChanges();
            }
        }

        private void AddPreset()
        {
            if (SelectedGroup != null)
            {
                var newPreset = new EffectPreset
                {
                    SerializedEffects = SerializeEffects(_combinedEffect.Effects)
                };
                SelectedGroup.Presets.Add(newPreset);
                UpdateDisplayedPresets();
                SelectedPreset = newPreset;
                StartEditing(newPreset);
            }
        }

        private void RemovePreset()
        {
            if (SelectedPreset != null)
            {
                foreach (var group in Groups)
                {
                    if (group.Presets.Remove(SelectedPreset))
                    {
                        break;
                    }
                }
                UpdateDisplayedPresets();
                SaveChanges();
            }
        }

        private void ApplyPreset()
        {
            if (SelectedPreset != null)
            {
                try
                {
                    var effects = DeserializeEffects(SelectedPreset.SerializedEffects, _jsonOptions);
                    if (effects != null)
                    {
                        BeginEdit?.Invoke(this, EventArgs.Empty);
                        foreach (var prop in _itemProperties)
                        {
                            var targetEffect = (CombinedEffect)prop.PropertyOwner;
                            targetEffect.Effects = effects;
                            targetEffect.SelectedPresetJson = JsonSerializer.Serialize(SelectedPreset, _jsonOptions);
                        }
                        EndEdit?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"プリセットの適用に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void UpdatePreset()
        {
            if (SelectedPreset != null)
            {
                SelectedPreset.SerializedEffects = SerializeEffects(_combinedEffect.Effects);
                SaveChanges();
            }
        }

        private object? GetSerializableObject(object? obj, HashSet<object> visited)
        {
            if (obj == null) return null;
            if (visited.Contains(obj)) return null;

            visited.Add(obj);

            var type = obj.GetType();

            if (type.IsPrimitive || obj is string || obj is decimal || type.IsEnum)
            {
                visited.Remove(obj);
                return obj;
            }

            if (obj is JsonElement)
            {
                visited.Remove(obj);
                return obj;
            }

            if (obj is Type)
            {
                visited.Remove(obj);
                return null;
            }

            if (obj is IEnumerable collection && type != typeof(string))
            {
                var list = new List<object?>();
                foreach (var item in collection)
                {
                    list.Add(GetSerializableObject(item, visited));
                }
                visited.Remove(obj);
                return list;
            }

            if (type.IsClass || (type.IsValueType && !type.IsPrimitive))
            {
                var dict = new Dictionary<string, object?>();
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() == null && p.GetIndexParameters().Length == 0);

                foreach (var prop in properties)
                {
                    if (prop.PropertyType == typeof(Type)) continue;

                    var value = prop.GetValue(obj);
                    if (value is Type) continue;

                    dict[prop.Name] = GetSerializableObject(value, visited);
                }
                visited.Remove(obj);
                return dict;
            }

            visited.Remove(obj);
            return obj;
        }

        private string SerializeEffects(ImmutableList<IVideoEffect> effects)
        {
            var serializableList = effects
                .Select(effect => new SerializableEffect
                {
                    TypeName = effect.GetType().AssemblyQualifiedName,
                    Properties = GetSerializableObject(effect, new HashSet<object>()) as Dictionary<string, object?>
                })
                .ToList();

            return JsonSerializer.Serialize(serializableList, _jsonOptions);
        }

        private static void PopulateObjectFromJson(object obj, JsonElement element, JsonSerializerOptions options)
        {
            if (element.ValueKind != JsonValueKind.Object) return;

            var objProperties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToDictionary(p => p.Name);

            foreach (var jsonProp in element.EnumerateObject())
            {
                if (objProperties.TryGetValue(jsonProp.Name, out var targetProp))
                {
                    try
                    {
                        if (targetProp.CanWrite)
                        {
                            var deserializedValue = jsonProp.Value.Deserialize(targetProp.PropertyType, options);
                            targetProp.SetValue(obj, deserializedValue);
                        }
                        else
                        {
                            var existingInnerObject = targetProp.GetValue(obj);
                            if (existingInnerObject != null)
                            {
                                PopulateObjectFromJson(existingInnerObject, jsonProp.Value, options);
                            }
                        }
                    }
                    catch (Exception) { }
                }
            }
        }

        public static ImmutableList<IVideoEffect>? DeserializeEffects(string? json, JsonSerializerOptions options)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            var serializableList = JsonSerializer.Deserialize<List<SerializableEffect>>(json, options);
            if (serializableList == null) return null;

            var builder = ImmutableList.CreateBuilder<IVideoEffect>();
            foreach (var item in serializableList)
            {
                if (string.IsNullOrEmpty(item.TypeName)) continue;

                var type = Type.GetType(item.TypeName);
                if (type == null) continue;

                var effectInstance = (IVideoEffect)Activator.CreateInstance(type)!;
                if (item.Properties != null)
                {
                    var propertiesJson = JsonSerializer.Serialize(item.Properties, options);
                    var propertiesElement = JsonSerializer.Deserialize<JsonElement>(propertiesJson);
                    PopulateObjectFromJson(effectInstance, propertiesElement, options);
                }
                builder.Add(effectInstance);
            }
            return builder.ToImmutable();
        }


        private void ToggleFavorite(EffectPreset? preset)
        {
            if (preset != null)
            {
                preset.IsFavorite = !preset.IsFavorite;
                if (SelectedGroup?.Name == "お気に入り")
                {
                    UpdateDisplayedPresets();
                }
                SaveChanges();
            }
        }

        private void SaveChanges()
        {
            var groupsToSave = new ObservableCollection<PresetGroup>(Groups.Where(g => g.Name != "すべて" && g.Name != "お気に入り"));
            _presetService.Save(groupsToSave);
        }
    }
}