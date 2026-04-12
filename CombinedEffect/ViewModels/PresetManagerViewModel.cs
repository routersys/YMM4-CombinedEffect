using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services;
using CombinedEffect.Services.Interfaces;
using CombinedEffect.Views;
using CombinedEffect.Effect;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace CombinedEffect.ViewModels;

internal sealed class PresetManagerViewModel : ObservableBase, IDisposable
{
    private readonly ItemProperty[] _itemProperties;
    private readonly Effect.CombinedEffect _effect;
    private readonly IEffectSerializationService _serialization;
    private readonly IPresetPersistenceService _persistence;
    private readonly IRecentPresetService _recentService;
    private readonly Dictionary<Guid, PresetItemViewModel> _presetCache = [];

    private PresetGroup? _selectedGroup;
    private PresetItemViewModel? _selectedPreset;
    private string _searchText = string.Empty;
    private bool _disposed;

    public ObservableCollection<PresetGroup> Groups { get; } = [];
    public ObservableCollection<PresetItemViewModel> DisplayedPresets { get; } = [];

    public PresetGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (!SetProperty(ref _selectedGroup, value)) return;
            RefreshDisplayedPresets();
        }
    }

    public PresetItemViewModel? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (!SetProperty(ref _selectedPreset, value)) return;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;
            OnPropertyChanged(nameof(IsSearchTextEmpty));
            RefreshDisplayedPresets();
        }
    }

    public bool IsSearchTextEmpty => string.IsNullOrEmpty(_searchText);
    public bool IsCurrentGroupVirtual => IsVirtualGroup(_selectedGroup);

    public ICommand AddGroupCommand { get; }
    public ICommand RemoveGroupCommand { get; }
    public ICommand RenameGroupCommand { get; }
    public ICommand AddPresetCommand { get; }
    public ICommand RemovePresetCommand { get; }
    public ICommand RenamePresetCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand UpdatePresetCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public PresetManagerViewModel(ItemProperty[] itemProperties)
    {
        _itemProperties = itemProperties;
        _effect = (Effect.CombinedEffect)itemProperties[0].PropertyOwner;
        _serialization = ServiceRegistry.Instance.EffectSerialization;
        _persistence = ServiceRegistry.Instance.PresetPersistence;
        _recentService = ServiceRegistry.Instance.RecentPreset;

        ServiceRegistry.Instance.PresetMigration.MigrateIfRequired();

        AddGroupCommand = new RelayCommand<object>(_ => ExecuteAddGroup());
        RemoveGroupCommand = new RelayCommand<object>(_ => ExecuteRemoveGroup(), _ => CanRemoveGroup());
        RenameGroupCommand = new RelayCommand<PresetGroup>(ExecuteRenameGroup, CanRenameGroup);
        AddPresetCommand = new RelayCommand<object>(_ => ExecuteAddPreset());
        RemovePresetCommand = new RelayCommand<object>(_ => ExecuteRemovePreset(), _ => _selectedPreset is not null);
        RenamePresetCommand = new RelayCommand<PresetItemViewModel>(ExecuteRenamePreset);
        ApplyPresetCommand = new RelayCommand<object>(_ => ExecuteApplyPreset(), _ => _selectedPreset is not null);
        UpdatePresetCommand = new RelayCommand<object>(_ => ExecuteUpdatePreset(), _ => CanUpdatePreset());
        ToggleFavoriteCommand = new RelayCommand<PresetItemViewModel>(ExecuteToggleFavorite);

        LoadData();
        SelectedGroup = Groups.FirstOrDefault(g => g.Name == Texts.PresetManager_GroupAll);
        ResourceRegistry.Instance.Register(this);
    }

    internal static bool IsVirtualGroup(PresetGroup? group) =>
        group?.Name is { } name &&
        (name == Texts.PresetManager_GroupAll || name == Texts.PresetManager_GroupFavorites || name == Texts.PresetManager_GroupRecent);

    private bool CanRemoveGroup() =>
        _selectedGroup is not null &&
        !IsVirtualGroup(_selectedGroup) &&
        _selectedGroup.Name != Texts.PresetManager_DefaultGroup;

    private static bool CanRenameGroup(PresetGroup? group) =>
        group is not null && !IsVirtualGroup(group);

    private bool CanUpdatePreset()
    {
        if (_selectedPreset is null) return false;
        try
        {
            if (string.IsNullOrEmpty(_effect.SelectedPresetJson)) return false;
            var appliedPreset = JsonConvert.DeserializeObject<EffectPreset>(_effect.SelectedPresetJson);
            if (appliedPreset is null || appliedPreset.Id != _selectedPreset.Model.Id) return false;
        }
        catch
        {
            return false;
        }
        var currentSerialized = _serialization.Serialize(_effect.Effects);
        return currentSerialized != _selectedPreset.Model.SerializedEffects;
    }

    private void LoadData()
    {
        Groups.Add(new PresetGroup { Name = Texts.PresetManager_GroupAll });
        Groups.Add(new PresetGroup { Name = Texts.PresetManager_GroupRecent });
        Groups.Add(new PresetGroup { Name = Texts.PresetManager_GroupFavorites });

        var registry = _persistence.LoadGroupRegistry();
        if (registry.Groups.Count == 0)
            registry.Groups.Add(new PresetGroup { Name = Texts.PresetManager_DefaultGroup });

        foreach (var group in registry.Groups)
        {
            foreach (var id in group.PresetIds)
            {
                var preset = _persistence.LoadPreset(id);
                if (preset is not null)
                    _presetCache[id] = new PresetItemViewModel(preset, _serialization);
            }
            Groups.Add(group);
        }
    }

    private void RefreshDisplayedPresets()
    {
        DisplayedPresets.Clear();
        if (_selectedGroup is null) return;

        OnPropertyChanged(nameof(IsCurrentGroupVirtual));

        IEnumerable<PresetItemViewModel> source = ResolvePresetsForGroup(_selectedGroup);
        if (!IsSearchTextEmpty)
            source = source.Where(p => p.Model.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        if (IsVirtualGroup(_selectedGroup) && _selectedGroup.Name != Texts.PresetManager_GroupRecent)
        {
            foreach (var preset in source.OrderBy(p => p.Model.Name))
                DisplayedPresets.Add(preset);
        }
        else
        {
            foreach (var preset in source)
                DisplayedPresets.Add(preset);
        }
    }

    private IEnumerable<PresetItemViewModel> ResolvePresetsForGroup(PresetGroup group)
    {
        if (group.Name == Texts.PresetManager_GroupAll)
            return Groups.Where(g => !IsVirtualGroup(g)).SelectMany(g => ResolvePresetsById(g.PresetIds));
        if (group.Name == Texts.PresetManager_GroupFavorites)
            return Groups.Where(g => !IsVirtualGroup(g))
                         .SelectMany(g => ResolvePresetsById(g.PresetIds))
                         .Where(p => p.Model.IsFavorite);
        if (group.Name == Texts.PresetManager_GroupRecent)
            return ResolvePresetsById(_recentService.GetRecentIds().ToList());
        return ResolvePresetsById(group.PresetIds);
    }

    private IEnumerable<PresetItemViewModel> ResolvePresetsById(List<Guid> ids) =>
        ids.Select(id => _presetCache.TryGetValue(id, out var p) ? p : null)
           .OfType<PresetItemViewModel>();

    private void ExecuteAddGroup()
    {
        var inputWindow = new InputDialogWindow(Texts.Dialog_InputName, Texts.Dialog_Title, Texts.PresetManager_NewGroup);
        if (inputWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(inputWindow.InputText)) return;

        var group = new PresetGroup { Name = inputWindow.InputText };
        Groups.Add(group);
        PersistRegistry();
        SelectedGroup = group;
    }

    private void ExecuteRenameGroup(PresetGroup? group)
    {
        var target = group ?? _selectedGroup;
        if (target is null || IsVirtualGroup(target)) return;

        var inputWindow = new InputDialogWindow(Texts.Dialog_InputName, Texts.Dialog_Title, target.Name);
        if (inputWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(inputWindow.InputText)) return;

        target.Name = inputWindow.InputText;
        PersistRegistry();
    }

    private void ExecuteRemoveGroup()
    {
        if (_selectedGroup is null || !CanRemoveGroup()) return;
        var confirmWindow = new ConfirmationDialogWindow(Texts.Confirm_DeleteGroup, Texts.Confirm_Delete);
        if (confirmWindow.ShowDialog() != true) return;

        foreach (var id in _selectedGroup.PresetIds.ToList())
            PurgePreset(id);
        Groups.Remove(_selectedGroup);
        SelectedGroup = Groups.FirstOrDefault(g => g.Name == Texts.PresetManager_GroupAll);
        PersistRegistry();
    }

    private void ExecuteAddPreset()
    {
        PresetGroup? targetGroup = _selectedGroup;
        if (targetGroup is null || IsVirtualGroup(targetGroup))
        {
            targetGroup = Groups.FirstOrDefault(g => !IsVirtualGroup(g));
            if (targetGroup is null) return;
        }

        var inputWindow = new InputDialogWindow(Texts.Dialog_InputName, Texts.Dialog_Title, Texts.PresetManager_NewPreset);
        if (inputWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(inputWindow.InputText)) return;

        var preset = new EffectPreset
        {
            Name = inputWindow.InputText,
            SerializedEffects = _serialization.Serialize(_effect.Effects),
        };
        var vm = new PresetItemViewModel(preset, _serialization);
        _presetCache[preset.Id] = vm;
        targetGroup.PresetIds.Add(preset.Id);
        _persistence.SavePreset(preset);
        PersistRegistry();
        SelectedGroup = targetGroup;
        RefreshDisplayedPresets();
        SelectedPreset = vm;
    }

    private void ExecuteRenamePreset(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? _selectedPreset;
        if (target is null) return;

        var inputWindow = new InputDialogWindow(Texts.Dialog_InputName, Texts.Dialog_Title, target.Model.Name);
        if (inputWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(inputWindow.InputText)) return;

        target.Model.Name = inputWindow.InputText;
        target.RefreshName();
        _persistence.SavePreset(target.Model);
    }

    private void ExecuteRemovePreset()
    {
        if (_selectedPreset is null) return;
        var confirmWindow = new ConfirmationDialogWindow(Texts.Confirm_DeletePreset, Texts.Confirm_Delete);
        if (confirmWindow.ShowDialog() != true) return;

        var id = _selectedPreset.Model.Id;
        foreach (var group in Groups.Where(g => !IsVirtualGroup(g)))
            group.PresetIds.Remove(id);
        PurgePreset(id);
        RefreshDisplayedPresets();
        PersistRegistry();
    }

    private void ExecuteApplyPreset()
    {
        if (_selectedPreset is null) return;
        try
        {
            var effects = _serialization.Deserialize(_selectedPreset.Model.SerializedEffects);
            if (effects is null) return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            var presetJson = JsonConvert.SerializeObject(_selectedPreset.Model);
            foreach (var prop in _itemProperties)
            {
                var target = (Effect.CombinedEffect)prop.PropertyOwner;
                target.Effects = effects;
                target.SelectedPresetJson = presetJson;
            }
            EndEdit?.Invoke(this, EventArgs.Empty);

            _recentService.Add(_selectedPreset.Model.Id);
            if (_selectedGroup?.Name == Texts.PresetManager_GroupRecent)
                RefreshDisplayedPresets();
        }
        catch
        {
            MessageBox.Show(Texts.PresetManager_ApplyError);
        }
    }

    private void ExecuteUpdatePreset()
    {
        if (_selectedPreset is null) return;
        _selectedPreset.Model.SerializedEffects = _serialization.Serialize(_effect.Effects);
        _selectedPreset.RefreshEffectInfo(_serialization);
        _persistence.SavePreset(_selectedPreset.Model);
        CommandManager.InvalidateRequerySuggested();
    }

    private void ExecuteToggleFavorite(PresetItemViewModel? presetVm)
    {
        if (presetVm is null) return;
        presetVm.Model.IsFavorite = !presetVm.Model.IsFavorite;
        presetVm.RefreshFavorite();
        _persistence.SavePreset(presetVm.Model);
        if (_selectedGroup?.Name == Texts.PresetManager_GroupFavorites)
            RefreshDisplayedPresets();
    }

    public void MoveGroup(PresetGroup source, PresetGroup target)
    {
        if (IsVirtualGroup(source) || IsVirtualGroup(target)) return;
        var sourceIdx = Groups.IndexOf(source);
        var targetIdx = Groups.IndexOf(target);
        if (sourceIdx < 0 || targetIdx < 0 || sourceIdx == targetIdx) return;
        Groups.Move(sourceIdx, targetIdx);
        PersistRegistry();
    }

    public void MovePreset(PresetItemViewModel source, PresetItemViewModel target)
    {
        var ownerGroup = FindOwnerGroup(source.Model);
        if (ownerGroup is null || !ownerGroup.PresetIds.Contains(target.Model.Id)) return;
        var sourceIdx = ownerGroup.PresetIds.IndexOf(source.Model.Id);
        var targetIdx = ownerGroup.PresetIds.IndexOf(target.Model.Id);
        if (sourceIdx < 0 || targetIdx < 0 || sourceIdx == targetIdx) return;
        ownerGroup.PresetIds.RemoveAt(sourceIdx);
        ownerGroup.PresetIds.Insert(targetIdx, source.Model.Id);
        RefreshDisplayedPresets();
        PersistRegistry();
    }

    private PresetGroup? FindOwnerGroup(EffectPreset preset) =>
        Groups.FirstOrDefault(g => !IsVirtualGroup(g) && g.PresetIds.Contains(preset.Id));

    private void PurgePreset(Guid id)
    {
        _presetCache.Remove(id);
        _persistence.DeletePreset(id);
        _recentService.Remove(id);
    }

    private void PersistRegistry()
    {
        var registry = new GroupRegistry
        {
            Groups = [.. Groups.Where(g => !IsVirtualGroup(g))]
        };
        _persistence.SaveGroupRegistry(registry);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ResourceRegistry.Instance.Unregister(this);
    }
}