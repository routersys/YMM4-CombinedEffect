using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services;
using CombinedEffect.Services.Interfaces;
using CombinedEffect.Views;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.ViewModels;

internal sealed class PresetManagerViewModel(ItemProperty[] itemProperties) : ObservableBase, IDisposable
{
    private readonly ItemProperty[] _itemProperties = itemProperties;
    private readonly Effect.CombinedEffect _effect = (Effect.CombinedEffect)itemProperties[0].PropertyOwner;
    private readonly Dictionary<Guid, Window> _historyWindows = new();
    private readonly IEffectSerializationService _serialization = ServiceRegistry.Instance.EffectSerialization;
    private readonly IPresetPersistenceService _persistence = ServiceRegistry.Instance.PresetPersistence;
    private readonly IRecentPresetService _recentService = ServiceRegistry.Instance.RecentPreset;
    private readonly IPresetExchangeService _presetExchange = ServiceRegistry.Instance.PresetExchange;
    private readonly IPresetExchangeDialogService _presetExchangeDialog = ServiceRegistry.Instance.PresetExchangeDialog;
    private readonly Dictionary<Guid, PresetItemViewModel> _presetCache = new();

    private readonly AsyncDebouncer _updateDebouncer = new();
    private ImmutableList<IVideoEffect> _trackedEffects = [];
    private volatile bool _canUpdatePresetCache;

    private bool _disposed;
    private Guid? _appliedPresetId;
    private Regex? _searchRegex;

    public ObservableCollection<PresetGroup> Groups { get; } = [];
    public ObservableCollection<PresetItemViewModel> DisplayedPresets { get; } = [];

    public PresetGroup? SelectedGroup
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            RefreshDisplayedPresets();
        }
    }

    private PresetSearchMode _searchMode = PresetSearchMode.Name;
    public PresetSearchMode SearchMode
    {
        get => _searchMode;
        set
        {
            if (_searchMode == value) return;
            _searchMode = value;
            OnPropertyChanged(nameof(SearchMode));
            OnPropertyChanged(nameof(IsSearchModeName));
            OnPropertyChanged(nameof(IsSearchModeEffectName));
            OnPropertyChanged(nameof(IsSearchModeEffectCount));
            OnPropertyChanged(nameof(IsSearchModeRawJson));
            OnPropertyChanged(nameof(IsSearchModeAny));
            RefreshDisplayedPresets();
        }
    }

    public bool IsSearchModeName => SearchMode == PresetSearchMode.Name;
    public bool IsSearchModeEffectName => SearchMode == PresetSearchMode.EffectName;
    public bool IsSearchModeEffectCount => SearchMode == PresetSearchMode.EffectCount;
    public bool IsSearchModeRawJson => SearchMode == PresetSearchMode.RawJson;
    public bool IsSearchModeAny => SearchMode == PresetSearchMode.Any;
    public string ExchangeExportText => Texts.PresetManager_ExchangeExport;
    public string ExchangeImportText => Texts.PresetManager_ExchangeImport;

    public PresetItemViewModel? SelectedPreset
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            TriggerUpdateCheck();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string SearchText
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            _searchRegex = TryBuildRegex(value);
            RefreshDisplayedPresets();
        }
    } = string.Empty;

    public bool IsCurrentGroupVirtual => IsVirtualGroup(SelectedGroup);

    public ICommand AddGroupCommand { get; private set; } = null!;
    public ICommand RemoveGroupCommand { get; private set; } = null!;
    public ICommand RenameGroupCommand { get; private set; } = null!;
    public ICommand AddPresetCommand { get; private set; } = null!;
    public ICommand RemovePresetCommand { get; private set; } = null!;
    public ICommand RenamePresetCommand { get; private set; } = null!;
    public ICommand ApplyPresetCommand { get; private set; } = null!;
    public ICommand UpdatePresetCommand { get; private set; } = null!;
    public ICommand ToggleFavoriteCommand { get; private set; } = null!;
    public ICommand ManageHistoryCommand { get; private set; } = null!;
    public ICommand ClearUnselectedCommand { get; private set; } = null!;
    public ICommand ApplySinglePresetCommand { get; private set; } = null!;
    public ICommand ClearPresetCommand { get; private set; } = null!;
    public ICommand SetSearchModeCommand { get; private set; } = null!;
    public ICommand ExportPresetsCommand { get; private set; } = null!;
    public ICommand ImportPresetsCommand { get; private set; } = null!;
    public ICommand CopyPresetCommand { get; private set; } = null!;
    public ICommand PastePresetCommand { get; private set; } = null!;
    public ICommand CutPresetCommand { get; private set; } = null!;

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public PresetManagerViewModel() : this([]) { }

    public void Initialize()
    {
        ServiceRegistry.Instance.PresetMigration.MigrateIfRequired();

        AddGroupCommand = new RelayCommand<object>(_ => ExecuteAddGroup());
        RemoveGroupCommand = new RelayCommand<object>(_ => ExecuteRemoveGroup(), _ => CanRemoveGroup());
        RenameGroupCommand = new RelayCommand<PresetGroup>(ExecuteRenameGroup, CanRenameGroup);
        AddPresetCommand = new RelayCommand<object>(_ => ExecuteAddPreset());
        RemovePresetCommand = new RelayCommand<IList>(ExecuteRemovePreset, list => ResolveExportTargets(list).Count > 0);
        RenamePresetCommand = new RelayCommand<PresetItemViewModel>(ExecuteRenamePreset, CanRenamePreset);
        ApplyPresetCommand = new RelayCommand<IList>(ExecuteApplyPreset, list => list is not null && list.Count > 0);
        UpdatePresetCommand = new RelayCommand<PresetItemViewModel>(ExecuteUpdatePreset, CanUpdatePreset);
        ToggleFavoriteCommand = new RelayCommand<PresetItemViewModel>(ExecuteToggleFavorite);
        ManageHistoryCommand = new RelayCommand<PresetItemViewModel>(ExecuteManageHistory);
        ClearUnselectedCommand = new RelayCommand<object>(_ => ExecuteClearUnselected());
        ApplySinglePresetCommand = new RelayCommand<PresetItemViewModel>(ExecuteApplySinglePreset);
        ClearPresetCommand = new RelayCommand<PresetItemViewModel>(ExecuteClearPreset, CanClearPreset);
        SetSearchModeCommand = new RelayCommand<object>(ExecuteSetSearchMode);
        ExportPresetsCommand = new RelayCommand<IList>(ExecuteExportPresets, CanExportPresets);
        ImportPresetsCommand = new RelayCommand<object>(_ => ExecuteImportPresets());
        CopyPresetCommand = new RelayCommand<IList>(ExecuteCopyPreset, list => ResolveExportTargets(list).Count > 0);
        PastePresetCommand = new RelayCommand<object>(_ => ExecutePastePreset(), _ => Clipboard.ContainsText());
        CutPresetCommand = new RelayCommand<IList>(ExecuteCutPreset, list => ResolveExportTargets(list).Count > 0);

        _effect.PropertyChanged += OnEffectPropertyChanged;
        AttachEffectHandlers(_effect.Effects);

        _ = LoadDataAsync();
        UpdateAppliedPresetId();
        TriggerUpdateCheck();
        ResourceRegistry.Instance.Register(this);
    }

    internal static bool IsVirtualGroup(PresetGroup? group) =>
        group?.Name is { } name &&
        (name == Texts.PresetManager_GroupAll || name == Texts.PresetManager_GroupFavorites || name == Texts.PresetManager_GroupRecent);

    private static Regex? TryBuildRegex(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return null;
        try { return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled); }
        catch { return null; }
    }

    private void AttachEffectHandlers(ImmutableList<IVideoEffect> effects)
    {
        foreach (var effect in _trackedEffects)
        {
            if (effect is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= OnVideoEffectPropertyChanged;
        }

        _trackedEffects = effects;

        foreach (var effect in _trackedEffects)
        {
            if (effect is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += OnVideoEffectPropertyChanged;
        }
    }

    private void OnVideoEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        TriggerUpdateCheck();
    }

    private void TriggerUpdateCheck()
    {
        var preset = SelectedPreset;

        _updateDebouncer.DebounceAsync("update_check", TimeSpan.FromMilliseconds(50), async () =>
        {
            if (preset is null)
            {
                SetUpdateCache(false);
                return;
            }

            var compare = await Task.Run(() =>
            {
                var currentStateJson = GetCurrentTabStateJson();
                var presetState = ResolvePresetTabState(preset.Model);
                var presetStateJson = EffectTabStateService.Serialize(presetState);
                return (Current: currentStateJson, Preset: presetStateJson);
            }).ConfigureAwait(false);

            var isDirty = !string.Equals(compare.Current, compare.Preset, StringComparison.Ordinal);
            SetUpdateCache(isDirty);
        });
    }

    private void SetUpdateCache(bool value)
    {
        if (_canUpdatePresetCache == value) return;
        _canUpdatePresetCache = value;
        Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
    }

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (e.PropertyName == nameof(Effect.CombinedEffect.SelectedPresetJson))
            {
                UpdateAppliedPresetId();
                TriggerUpdateCheck();
            }
            else if (e.PropertyName == nameof(Effect.CombinedEffect.Effects))
            {
                AttachEffectHandlers(_effect.Effects);
                TriggerUpdateCheck();
            }
            else if (e.PropertyName == nameof(Effect.CombinedEffect.EffectTabsJson))
            {
                TriggerUpdateCheck();
            }
        });
    }

    private void UpdateAppliedPresetId()
    {
        if (string.IsNullOrEmpty(_effect.SelectedPresetJson))
        {
            _appliedPresetId = null;
            return;
        }
        try
        {
            var preset = JsonConvert.DeserializeObject<EffectPreset>(_effect.SelectedPresetJson);
            _appliedPresetId = preset?.Id;
        }
        catch (Exception)
        {
            _appliedPresetId = null;
        }
    }

    private bool CanRemoveGroup() =>
        SelectedGroup is not null &&
        !IsVirtualGroup(SelectedGroup) &&
        SelectedGroup.Name != Texts.PresetManager_DefaultGroup;

    private static bool CanRenameGroup(PresetGroup? group) =>
        group is not null && !IsVirtualGroup(group);

    private bool CanRenamePreset(PresetItemViewModel? presetVm) => (presetVm ?? SelectedPreset) is not null;

    private bool CanUpdatePreset(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? SelectedPreset;
        return _canUpdatePresetCache && target == SelectedPreset;
    }

    private bool CanClearPreset(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? SelectedPreset;
        return target is not null && target.EffectCount > 0;
    }

    private async Task LoadDataAsync()
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Groups.Add(new PresetGroup { Name = Texts.PresetManager_GroupAll });
            Groups.Add(new PresetGroup { Name = Texts.PresetManager_GroupRecent });
            Groups.Add(new PresetGroup { Name = Texts.PresetManager_GroupFavorites });
        });

        var registry = await _persistence.LoadGroupRegistryAsync().ConfigureAwait(false);
        if (registry.Groups.Count == 0)
            registry.Groups.Add(new PresetGroup { Name = Texts.PresetManager_DefaultGroup });

        var loadedVms = new List<(PresetGroup group, List<PresetItemViewModel> vms)>();
        foreach (var group in registry.Groups)
        {
            var vms = new List<PresetItemViewModel>();
            foreach (var id in group.PresetIds)
            {
                var preset = await _persistence.LoadPresetAsync(id).ConfigureAwait(false);
                if (preset is not null)
                    vms.Add(new PresetItemViewModel(preset, _serialization));
            }
            loadedVms.Add((group, vms));
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var (group, vms) in loadedVms)
            {
                foreach (var vm in vms)
                    _presetCache[vm.Model.Id] = vm;
                Groups.Add(group);
            }
            SelectedGroup = Groups.FirstOrDefault(g => g.Name == Texts.PresetManager_GroupAll);
        });
    }

    private void RefreshDisplayedPresets()
    {
        DisplayedPresets.Clear();
        if (SelectedGroup is null) return;

        OnPropertyChanged(nameof(IsCurrentGroupVirtual));

        IEnumerable<PresetItemViewModel> source = ResolvePresetsForGroup(SelectedGroup);
        if (!string.IsNullOrWhiteSpace(SearchText))
            source = source.Where(p => MatchesSearch(p, SearchText, SearchMode, _searchRegex));

        if (IsVirtualGroup(SelectedGroup) && SelectedGroup.Name != Texts.PresetManager_GroupRecent)
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

    private static bool MatchesSearch(PresetItemViewModel p, string text, PresetSearchMode mode, Regex? regex)
    {
        bool IsMatchText(string? input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            if (regex is not null && regex.IsMatch(input)) return true;
            return input.Contains(text, StringComparison.OrdinalIgnoreCase);
        }

        return mode switch
        {
            PresetSearchMode.Name => IsMatchText(p.Model.Name),
            PresetSearchMode.EffectName => IsMatchText(p.EffectInfo),
            PresetSearchMode.EffectCount => int.TryParse(text, out var c) ? p.EffectCount == c : IsMatchText(p.EffectCount.ToString()),
            PresetSearchMode.RawJson => IsMatchText(p.Model.SerializedEffects) || IsMatchText(p.Model.SerializedTabs),
            PresetSearchMode.Any => IsMatchText(p.Model.Name) || IsMatchText(p.EffectInfo) || IsMatchText(p.Model.SerializedEffects) || IsMatchText(p.Model.SerializedTabs),
            _ => false
        };
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
        {
            var recentIds = _recentService.GetRecentIdsAsync().GetAwaiter().GetResult();
            return ResolvePresetsById(recentIds);
        }
        return ResolvePresetsById(group.PresetIds);
    }

    private List<PresetItemViewModel> ResolvePresetsById(IReadOnlyList<Guid> ids)
    {
        var result = new List<PresetItemViewModel>(ids.Count);
        foreach (var id in ids)
        {
            if (_presetCache.TryGetValue(id, out var p))
                result.Add(p);
        }
        return result;
    }

    private void ExecuteSetSearchMode(object? modeText)
    {
        if (modeText is string str && Enum.TryParse<PresetSearchMode>(str, out var mode))
            SearchMode = mode;
    }

    private bool CanExportPresets(IList? selectedItems) => ResolveExportTargets(selectedItems).Count > 0;

    private List<PresetItemViewModel> ResolveExportTargets(IList? selectedItems)
    {
        var targets = selectedItems?
            .OfType<PresetItemViewModel>()
            .GroupBy(p => p.Model.Id)
            .Select(g => g.First())
            .ToList() ?? [];

        if (targets.Count == 0 && SelectedPreset is not null)
            targets.Add(SelectedPreset);

        return targets;
    }

    private void ExecuteExportPresets(IList? selectedItems)
    {
        var targets = ResolveExportTargets(selectedItems);
        if (targets.Count == 0) return;

        var defaultFileName = targets.Count == 1
            ? CreateSafeFileName(targets[0].Model.Name)
            : Texts.PresetManager_ExchangeBundleDefaultName;

        var filePath = _presetExchangeDialog.ShowExportDialog(defaultFileName);
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            _presetExchange.Export(filePath, [.. targets.Select(p => p.Model)]);
        }
        catch (Exception)
        {
            MessageBox.Show(Texts.PresetManager_ExchangeExportError);
        }
    }

    private void ExecuteCopyPreset(IList? selectedItems)
    {
        var targets = ResolveExportTargets(selectedItems);
        if (targets.Count == 0) return;

        var package = new PresetExchangePackage
        {
            FormatId = PresetExchangeFormat.FormatId,
            Version = PresetExchangeFormat.Version,
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Presets = [.. targets.Select(p => new EffectPreset
            {
                Id = p.Model.Id,
                Name = p.Model.Name,
                IsFavorite = p.Model.IsFavorite,
                SerializedEffects = p.Model.SerializedEffects,
                SerializedTabs = p.Model.SerializedTabs,
            })]
        };
        var json = JsonConvert.SerializeObject(package, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        Clipboard.SetText(json);
    }

    private void ExecuteCutPreset(IList? selectedItems)
    {
        ExecuteCopyPreset(selectedItems);
        RemoveTargets(selectedItems, true);
    }

    private void ExecutePastePreset()
    {
        try
        {
            var json = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(json)) return;
            var package = JsonConvert.DeserializeObject<PresetExchangePackage>(json, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            if (package is null || package.FormatId != PresetExchangeFormat.FormatId) return;
            package = PresetExchangeMigrator.Migrate(package);
            if (package.Presets.Count == 0) return;

            var targetGroup = SelectedGroup;
            if (targetGroup is null || IsVirtualGroup(targetGroup))
                targetGroup = Groups.FirstOrDefault(g => !IsVirtualGroup(g));
            if (targetGroup is null) return;

            PresetItemViewModel? firstImported = null;
            foreach (var preset in package.Presets)
            {
                if (string.IsNullOrWhiteSpace(preset.Name))
                    preset.Name = Texts.PresetManager_NewPreset;

                preset.Id = Guid.NewGuid();
                var vm = new PresetItemViewModel(preset, _serialization);
                _presetCache[preset.Id] = vm;
                targetGroup.PresetIds.Add(preset.Id);
                _persistence.SavePreset(preset);
                firstImported ??= vm;
            }

            PersistRegistry();
            SelectedGroup = targetGroup;
            RefreshDisplayedPresets();
            if (firstImported is not null)
                SelectedPreset = firstImported;
        }
        catch (Exception)
        {
            MessageBox.Show(Texts.PresetManager_ExchangeImportError);
        }
    }

    private void ExecuteImportPresets()
    {
        var filePaths = _presetExchangeDialog.ShowImportDialog();
        if (filePaths.Count == 0) return;

        try
        {
            var importedPresets = _presetExchange.Import(filePaths);
            if (importedPresets.Count == 0) return;

            var targetGroup = SelectedGroup;
            if (targetGroup is null || IsVirtualGroup(targetGroup))
                targetGroup = Groups.FirstOrDefault(g => !IsVirtualGroup(g));
            if (targetGroup is null) return;

            PresetItemViewModel? firstImported = null;
            foreach (var preset in importedPresets)
            {
                if (string.IsNullOrWhiteSpace(preset.Name))
                    preset.Name = Texts.PresetManager_NewPreset;

                var vm = new PresetItemViewModel(preset, _serialization);
                _presetCache[preset.Id] = vm;
                targetGroup.PresetIds.Add(preset.Id);
                _persistence.SavePreset(preset);
                firstImported ??= vm;
            }

            PersistRegistry();
            SelectedGroup = targetGroup;
            RefreshDisplayedPresets();
            if (firstImported is not null)
                SelectedPreset = firstImported;
        }
        catch (Exception)
        {
            MessageBox.Show(Texts.PresetManager_ExchangeImportError);
        }
    }

    private static string CreateSafeFileName(string source)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = string.Concat(source.Select(ch => invalidChars.Contains(ch) ? '_' : ch)).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? Texts.PresetManager_NewPreset : safeName;
    }

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
        var target = group ?? SelectedGroup;
        if (target is null || IsVirtualGroup(target)) return;

        var inputWindow = new InputDialogWindow(Texts.Dialog_InputName, Texts.Dialog_Title, target.Name);
        if (inputWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(inputWindow.InputText)) return;

        target.Name = inputWindow.InputText;
        PersistRegistry();
    }

    private void ExecuteRemoveGroup()
    {
        if (SelectedGroup is null || !CanRemoveGroup()) return;
        var confirmWindow = new ConfirmationDialogWindow(Texts.Confirm_DeleteGroup, Texts.Confirm_Delete);
        if (confirmWindow.ShowDialog() != true) return;

        foreach (var id in SelectedGroup.PresetIds.ToList())
            PurgePreset(id);
        Groups.Remove(SelectedGroup);
        SelectedGroup = Groups.FirstOrDefault(g => g.Name == Texts.PresetManager_GroupAll);
        PersistRegistry();
    }

    private void ExecuteAddPreset()
    {
        PresetGroup? targetGroup = SelectedGroup;
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
            SerializedTabs = GetCurrentTabStateJson(),
            SerializedEffects = GetCurrentSelectedEffectsJson(),
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
        var target = presetVm ?? SelectedPreset;
        if (target is null) return;

        var inputWindow = new InputDialogWindow(Texts.Dialog_InputName, Texts.Dialog_Title, target.Model.Name);
        if (inputWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(inputWindow.InputText)) return;

        target.Model.Name = inputWindow.InputText;
        target.RefreshName();
        _persistence.SavePreset(target.Model);
    }

    private void ExecuteRemovePreset(IList? selectedItems) => RemoveTargets(selectedItems, false);

    private void RemoveTargets(IList? selectedItems, bool skipConfirm)
    {
        var targets = ResolveExportTargets(selectedItems);
        if (targets.Count == 0) return;

        if (!skipConfirm)
        {
            var confirmWindow = new ConfirmationDialogWindow(Texts.Confirm_DeletePreset, Texts.Confirm_Delete);
            if (confirmWindow.ShowDialog() != true) return;
        }

        var ids = targets.Select(t => t.Model.Id).ToList();
        foreach (var id in ids)
        {
            if (_historyWindows.TryGetValue(id, out var window))
                window.Close();
            foreach (var group in Groups.Where(g => !IsVirtualGroup(g)))
                group.PresetIds.Remove(id);
            if (SelectedPreset?.Model.Id == id)
                SelectedPreset = null;
            PurgePreset(id);
        }
        RefreshDisplayedPresets();
        PersistRegistry();
    }

    private void ExecuteApplyPreset(IList? selectedItems)
    {
        if (selectedItems is null || selectedItems.Count == 0) return;
        var presets = selectedItems.OfType<PresetItemViewModel>().ToList();
        if (presets.Count == 0) return;

        try
        {
            var isAppending = false;
            if (presets.Count == 1 && _effect.Effects.Count > 0)
            {
                var currentSerialized = _serialization.Serialize(_effect.Effects);
                var presetState = ResolvePresetTabState(presets[0].Model);
                var presetSelectedEffectsJson = EffectTabStateService.GetSelectedEffectsJson(presetState);
                if (currentSerialized == presetSelectedEffectsJson)
                {
                    var confirmWindow = new ConfirmationDialogWindow(Texts.Confirm_SamePresetApply, Texts.Confirm_SamePresetApplyTitle);
                    if (confirmWindow.ShowDialog() != true) return;
                    isAppending = true;
                }
            }

            var combinedEffects = new List<IVideoEffect>();
            EffectTabState stateToApply;
            string presetJson;

            if (presets.Count == 1 && !isAppending)
            {
                var sourceState = ResolvePresetTabState(presets[0].Model);
                stateToApply = sourceState;
                presetJson = JsonConvert.SerializeObject(presets[0].Model);
                combinedEffects.AddRange(EffectTabStateService.GetSelectedEffects(sourceState, _serialization));
            }
            else
            {
                foreach (var preset in presets)
                {
                    var sourceState = ResolvePresetTabState(preset.Model);
                    var effects = EffectTabStateService.GetSelectedEffects(sourceState, _serialization);
                    if (effects.Count > 0)
                        combinedEffects.AddRange(effects);
                }

                if (isAppending)
                    combinedEffects.InsertRange(0, _effect.Effects);

                var immutableCombined = ImmutableList.CreateRange(combinedEffects);
                stateToApply = EffectTabStateService.CreateDefault(immutableCombined, _serialization, Texts.EffectTab_FirstName);
                presetJson = presets.Count == 1 ? JsonConvert.SerializeObject(presets[0].Model) : string.Empty;
            }

            var immutableEffects = ImmutableList.CreateRange(combinedEffects);
            var tabStateJson = EffectTabStateService.Serialize(stateToApply);

            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var prop in _itemProperties)
            {
                var target = (Effect.CombinedEffect)prop.PropertyOwner;
                target.EffectTabsJson = tabStateJson;
                target.Effects = immutableEffects;
                target.SelectedPresetJson = presetJson;
            }
            EndEdit?.Invoke(this, EventArgs.Empty);

            if (presets.Count == 1)
                _appliedPresetId = presets[0].Model.Id;

            foreach (var preset in presets)
                _recentService.Add(preset.Model.Id);

            if (SelectedGroup?.Name == Texts.PresetManager_GroupRecent)
                RefreshDisplayedPresets();

            TriggerUpdateCheck();
        }
        catch (Exception)
        {
            MessageBox.Show(Texts.PresetManager_ApplyError);
        }
    }

    private void ExecuteApplySinglePreset(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? SelectedPreset;
        if (target is not null)
            ExecuteApplyPreset(new[] { target });
    }

    private void ExecuteManageHistory(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? SelectedPreset;
        if (target is null) return;

        var id = target.Model.Id;
        if (_historyWindows.TryGetValue(id, out var existingWindow))
        {
            if (existingWindow.IsVisible)
            {
                existingWindow.Focus();
                return;
            }
            _historyWindows.Remove(id);
        }

        var vm = new HistoryManagerViewModel(target, _itemProperties);
        vm.BeginEdit += (_, _) => BeginEdit?.Invoke(this, EventArgs.Empty);
        vm.EndEdit += (_, _) => EndEdit?.Invoke(this, EventArgs.Empty);
        var window = new HistoryManagerWindow(vm) { Owner = Application.Current.MainWindow };
        window.Closed += (s, e) =>
        {
            _historyWindows.Remove(id);
            vm.Dispose();
        };
        _historyWindows[id] = window;
        window.Show();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ExecuteClearUnselected()
    {
        var confirmWindow = new ConfirmationDialogWindow(Texts.Confirm_ClearUnselected, Texts.Confirm_ClearUnselectedTitle);
        if (confirmWindow.ShowDialog() != true) return;

        var currentEffects = _effect.Effects;
        var newEffects = currentEffects.Where(e => e.IsEnabled).ToList();
        if (currentEffects.Count == newEffects.Count) return;

        BeginEdit?.Invoke(this, EventArgs.Empty);
        var immutableEffects = ImmutableList.CreateRange(newEffects);
        foreach (var prop in _itemProperties)
        {
            var target = (Effect.CombinedEffect)prop.PropertyOwner;
            target.Effects = immutableEffects;
        }
        EndEdit?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteClearPreset(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? SelectedPreset;
        if (target is null) return;

        var confirmWindow = new ConfirmationDialogWindow(Texts.Confirm_ClearPreset, Texts.Confirm_ClearPresetTitle);
        if (confirmWindow.ShowDialog() != true) return;

        var emptyState = EffectTabStateService.CreateDefault(ImmutableList<IVideoEffect>.Empty, _serialization, Texts.EffectTab_FirstName);
        target.Model.SerializedTabs = EffectTabStateService.Serialize(emptyState);
        target.Model.SerializedEffects = EffectTabStateService.GetSelectedEffectsJson(emptyState);
        target.RefreshEffectInfo(_serialization);
        _persistence.SavePreset(target.Model);

        TriggerUpdateCheck();
    }

    private void ExecuteUpdatePreset(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? SelectedPreset;
        if (target is null) return;
        target.Model.SerializedTabs = GetCurrentTabStateJson();
        target.Model.SerializedEffects = GetCurrentSelectedEffectsJson();
        target.RefreshEffectInfo(_serialization);
        _persistence.SavePreset(target.Model);

        if (_appliedPresetId == target.Model.Id)
        {
            var json = JsonConvert.SerializeObject(target.Model);
            foreach (var prop in _itemProperties)
            {
                var owner = (Effect.CombinedEffect)prop.PropertyOwner;
                owner.SelectedPresetJson = json;
            }
        }

        TriggerUpdateCheck();
    }

    private EffectTabState GetCurrentTabState()
    {
        EffectTabState? parsed = null;
        if (EffectTabStateService.TryDeserialize(_effect.EffectTabsJson, out var state))
            parsed = state;

        return EffectTabStateService.Normalize(parsed, _effect.Effects, _serialization, Texts.EffectTab_FirstName);
    }

    private string GetCurrentTabStateJson() =>
        EffectTabStateService.Serialize(GetCurrentTabState());

    private string GetCurrentSelectedEffectsJson() =>
        EffectTabStateService.GetSelectedEffectsJson(GetCurrentTabState());

    private EffectTabState ResolvePresetTabState(EffectPreset preset)
    {
        EffectTabState? parsed = null;
        if (EffectTabStateService.TryDeserialize(preset.SerializedTabs, out var state))
            parsed = state;
        else if (!string.IsNullOrWhiteSpace(preset.SerializedEffects))
        {
            parsed = EffectTabStateService.CreateSingleTabState(preset.SerializedEffects, Texts.EffectTab_FirstName);
            var tab = parsed.Tabs[0];
            tab.Id = preset.Id == Guid.Empty ? tab.Id : preset.Id;
            parsed.SelectedTabId = tab.Id;
        }

        return EffectTabStateService.Normalize(parsed, ImmutableList<IVideoEffect>.Empty, _serialization, Texts.EffectTab_FirstName);
    }

    private void ExecuteToggleFavorite(PresetItemViewModel? presetVm)
    {
        if (presetVm is null) return;
        presetVm.Model.IsFavorite = !presetVm.Model.IsFavorite;
        presetVm.RefreshFavorite();
        _persistence.SavePreset(presetVm.Model);
        if (SelectedGroup?.Name == Texts.PresetManager_GroupFavorites)
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
        _updateDebouncer.Dispose();
        foreach (var effect in _trackedEffects)
        {
            if (effect is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= OnVideoEffectPropertyChanged;
        }
        _effect.PropertyChanged -= OnEffectPropertyChanged;
        ResourceRegistry.Instance.Unregister(this);
    }
}