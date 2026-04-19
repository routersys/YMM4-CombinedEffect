using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.ViewModels;

internal sealed class PresetManagerViewModel(ItemProperty[] itemProperties) : ObservableBase, IDisposable
{
    private sealed class EvaluatedStateSnapshot(Guid presetId, string currentStateJson)
    {
        public Guid PresetId { get; } = presetId;
        public string CurrentStateJson { get; } = currentStateJson;
    }

    private readonly ItemProperty[] _itemProperties = itemProperties;
    private readonly Effect.CombinedEffect _effect = (Effect.CombinedEffect)itemProperties[0].PropertyOwner;
    private readonly IEffectSerializationService _serialization = ServiceRegistry.Instance.EffectSerialization;
    private readonly IPresetPersistenceService _persistence = ServiceRegistry.Instance.PresetPersistence;
    private readonly IRecentPresetService _recentService = ServiceRegistry.Instance.RecentPreset;
    private readonly IPresetExchangeService _presetExchange = ServiceRegistry.Instance.PresetExchange;
    private readonly IPresetExchangeDialogService _presetExchangeDialog = ServiceRegistry.Instance.PresetExchangeDialog;
    private readonly IUserDialogService _dialog = ServiceRegistry.Instance.UserDialog;
    private readonly IHistoryWindowService _historyWindowService = ServiceRegistry.Instance.HistoryWindow;
    private readonly ILoggerService _logger = ServiceRegistry.Instance.Logger;
    private readonly IResilienceService _resilience = ServiceRegistry.Instance.Resilience;
    private readonly IPresetApplyPlannerService _presetApplyPlanner = ServiceRegistry.Instance.PresetApplyPlanner;
    private readonly Dictionary<Guid, PresetItemViewModel> _presetCache = new();

    private readonly AsyncDebouncer _updateDebouncer = new();
    private ImmutableList<IVideoEffect> _trackedEffects = [];
    private volatile bool _canUpdatePresetCache;
    private bool _hasPotentialUnnotifiedEffectMutation;
    private DispatcherOperation? _pendingInputDrivenCheckOperation;
    private EvaluatedStateSnapshot? _lastEvaluatedSnapshot;

    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;

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
        InputManager.Current.PostProcessInput += OnPostProcessInput;
        ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
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

    private bool ExecuteWithRetry(string operationName, Action action, string? userMessage = null, int maxAttempts = 3)
    {
        try
        {
            _resilience.Execute(operationName, action, maxAttempts);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(operationName, ex);
            if (!string.IsNullOrWhiteSpace(userMessage))
                Application.Current.Dispatcher.Invoke(() => _dialog.ShowMessage(userMessage));
            return false;
        }
    }

    private T? ExecuteWithRetry<T>(string operationName, Func<T> action, string? userMessage = null, int maxAttempts = 3)
    {
        try
        {
            return _resilience.Execute(operationName, action, maxAttempts);
        }
        catch (Exception ex)
        {
            _logger.LogError(operationName, ex);
            if (!string.IsNullOrWhiteSpace(userMessage))
                Application.Current.Dispatcher.Invoke(() => _dialog.ShowMessage(userMessage));
            return default;
        }
    }

    private async Task<T?> ExecuteWithRetryAsync<T>(string operationName, Func<Task<T>> action, string? userMessage = null, int maxAttempts = 3)
    {
        try
        {
            return await _resilience.ExecuteAsync(operationName, action, maxAttempts).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(operationName, ex);
            if (!string.IsNullOrWhiteSpace(userMessage))
                Application.Current.Dispatcher.Invoke(() => _dialog.ShowMessage(userMessage));
            return default;
        }
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

    private void OnPostProcessInput(object? sender, ProcessInputEventArgs e)
    {
        if (_disposed)
            return;

        var input = e.StagingItem.Input;
        if (input is MouseButtonEventArgs mouseButtonEvent &&
            mouseButtonEvent.ChangedButton == MouseButton.Left &&
            mouseButtonEvent.ButtonState == MouseButtonState.Pressed)
        {
            _hasPotentialUnnotifiedEffectMutation = true;
            return;
        }

        if (input is MouseEventArgs mouseMove)
        {
            if (mouseMove.LeftButton == MouseButtonState.Pressed)
                _hasPotentialUnnotifiedEffectMutation = true;
            return;
        }

        if (input is not MouseButtonEventArgs mouseButton)
            return;
        if (mouseButton.ChangedButton != MouseButton.Left || mouseButton.ButtonState != MouseButtonState.Released)
            return;

        QueueInputDrivenUpdateCheck();
    }

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (_disposed)
            return;

        switch ((int)msg.message)
        {
            case WmLeftButtonDown:
                _hasPotentialUnnotifiedEffectMutation = true;
                break;
            case WmLeftButtonUp:
                QueueInputDrivenUpdateCheck();
                break;
        }
    }

    private void QueueInputDrivenUpdateCheck()
    {
        if (!_hasPotentialUnnotifiedEffectMutation)
            return;

        if (_pendingInputDrivenCheckOperation is not null &&
            _pendingInputDrivenCheckOperation.Status is DispatcherOperationStatus.Pending or DispatcherOperationStatus.Executing)
            return;

        _pendingInputDrivenCheckOperation = Application.Current.Dispatcher.InvokeAsync(
            ExecuteInputDrivenUpdateCheck,
            DispatcherPriority.ContextIdle);
    }

    private void ExecuteInputDrivenUpdateCheck()
    {
        _pendingInputDrivenCheckOperation = null;

        var shouldCheck = _hasPotentialUnnotifiedEffectMutation;
        _hasPotentialUnnotifiedEffectMutation = false;
        if (!shouldCheck)
            return;

        var selected = SelectedPreset;
        if (selected is null)
            return;
        if (_appliedPresetId is not Guid appliedPresetId || selected.Model.Id != appliedPresetId)
            return;

        var currentStateJson = GetCurrentTabStateJson();
        var snapshot = Volatile.Read(ref _lastEvaluatedSnapshot);
        if (snapshot is not null &&
            snapshot.PresetId == selected.Model.Id &&
            string.Equals(currentStateJson, snapshot.CurrentStateJson, StringComparison.Ordinal))
            return;

        TriggerUpdateCheck();
    }

    private void TriggerUpdateCheck()
    {
        var preset = SelectedPreset;

        _updateDebouncer.DebounceAsync("update_check", TimeSpan.FromMilliseconds(50), async () =>
        {
            try
            {
                if (preset is null)
                {
                    SetUpdateCache(false);
                    return;
                }

                var selectedPresetId = preset.Model.Id;
                if (_appliedPresetId is not Guid appliedPresetId || appliedPresetId != selectedPresetId)
                {
                    SetUpdateCache(false);
                    return;
                }

                var lastSnapshot = Volatile.Read(ref _lastEvaluatedSnapshot);

                var compare = await Task.Run(() =>
                {
                    var currentStateJson = GetCurrentTabStateJson();

                    if (lastSnapshot is not null &&
                        lastSnapshot.PresetId == selectedPresetId &&
                        string.Equals(currentStateJson, lastSnapshot.CurrentStateJson, StringComparison.Ordinal))
                    {
                        return (Skipped: true, Current: currentStateJson, Preset: string.Empty);
                    }

                    var presetState = EffectTabStateService.ResolvePresetState(preset.Model, _serialization, Texts.EffectTab_FirstName);
                    var presetStateJson = EffectTabStateService.Serialize(presetState);
                    return (Skipped: false, Current: currentStateJson, Preset: presetStateJson);
                }).ConfigureAwait(false);

                var isStillTarget = await Application.Current.Dispatcher.InvokeAsync(() =>
                    SelectedPreset?.Model.Id == selectedPresetId &&
                    _appliedPresetId is Guid currentAppliedId &&
                    currentAppliedId == selectedPresetId);
                if (!isStillTarget)
                {
                    SetUpdateCache(false);
                    return;
                }

                if (compare.Skipped)
                    return;

                var isDirty = !string.Equals(compare.Current, compare.Preset, StringComparison.Ordinal);
                Volatile.Write(ref _lastEvaluatedSnapshot, new EvaluatedStateSnapshot(selectedPresetId, compare.Current));
                SetUpdateCache(isDirty);
            }
            catch (Exception ex)
            {
                _logger.LogError("Update check failed.", ex);
                SetUpdateCache(false);
            }
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
        catch (Exception ex)
        {
            _logger.LogError("Failed to parse selected preset JSON.", ex);
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
        if (!_canUpdatePresetCache || target is null || target != SelectedPreset)
            return false;
        return _appliedPresetId is Guid appliedPresetId && target.Model.Id == appliedPresetId;
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

        var registry = await ExecuteWithRetryAsync("Load preset group registry", () => _persistence.LoadGroupRegistryAsync(), maxAttempts: 3).ConfigureAwait(false)
            ?? new GroupRegistry();

        if (registry.Groups.Count == 0)
            registry.Groups.Add(new PresetGroup { Name = Texts.PresetManager_DefaultGroup });

        var loadedVms = new List<(PresetGroup group, List<PresetItemViewModel> vms)>();
        foreach (var group in registry.Groups)
        {
            var vms = new List<PresetItemViewModel>();
            foreach (var id in group.PresetIds)
            {
                var preset = await ExecuteWithRetryAsync($"Load preset: {id}", () => _persistence.LoadPresetAsync(id), maxAttempts: 2).ConfigureAwait(false);
                if (preset is not null)
                    vms.Add(CreatePresetItem(preset));
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
            var recentIds = ExecuteWithRetry("Load recent preset ids", () => _recentService.GetRecentIdsAsync().GetAwaiter().GetResult(), maxAttempts: 2)
                ?? Array.Empty<Guid>();
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

        ExecuteWithRetry(
            "Export presets",
            () => _presetExchange.Export(filePath, [.. targets.Select(p => p.Model)]),
            Texts.PresetManager_ExchangeExportError,
            3);
    }

    private void ExecuteCopyPreset(IList? selectedItems)
    {
        var targets = ResolveExportTargets(selectedItems);
        if (targets.Count == 0) return;

        ExecuteWithRetry(
            "Copy presets to clipboard",
            () =>
            {
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
            },
            Texts.PresetManager_ExchangeExportError,
            2);
    }

    private void ExecuteCutPreset(IList? selectedItems)
    {
        ExecuteCopyPreset(selectedItems);
        RemoveTargets(selectedItems, true);
    }

    private void ExecutePastePreset()
    {
        ExecuteWithRetry(
            "Paste presets",
            () =>
            {
                var json = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var package = JsonConvert.DeserializeObject<PresetExchangePackage>(json, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                if (package is null || package.FormatId != PresetExchangeFormat.FormatId)
                    return;

                package = PresetExchangeMigrator.Migrate(package);
                if (package.Presets.Count == 0)
                    return;

                var targetGroup = SelectedGroup;
                if (targetGroup is null || IsVirtualGroup(targetGroup))
                    targetGroup = Groups.FirstOrDefault(g => !IsVirtualGroup(g));
                if (targetGroup is null)
                    return;

                var stagedPresets = new List<EffectPreset>(package.Presets.Count);
                var stagedItems = new List<PresetItemViewModel>(package.Presets.Count);
                PresetItemViewModel? firstImported = null;
                try
                {
                    foreach (var preset in package.Presets)
                    {
                        if (string.IsNullOrWhiteSpace(preset.Name))
                            preset.Name = Texts.PresetManager_NewPreset;

                        preset.Id = Guid.NewGuid();
                        _resilience.Execute($"Save pasted preset: {preset.Id}", () => _persistence.SavePreset(preset), 3);
                        stagedPresets.Add(preset);

                        var vm = CreatePresetItem(preset);
                        stagedItems.Add(vm);
                        firstImported ??= vm;
                    }
                }
                catch
                {
                    foreach (var stagedPreset in stagedPresets)
                        _resilience.Execute($"Rollback pasted preset: {stagedPreset.Id}", () => _persistence.DeletePreset(stagedPreset.Id), 2);
                    throw;
                }

                foreach (var vm in stagedItems)
                {
                    _presetCache[vm.Model.Id] = vm;
                    targetGroup.PresetIds.Add(vm.Model.Id);
                }

                PersistRegistry();
                SelectedGroup = targetGroup;
                RefreshDisplayedPresets();
                if (firstImported is not null)
                    SelectedPreset = firstImported;
            },
            Texts.PresetManager_ExchangeImportError,
            1);
    }

    private void ExecuteImportPresets()
    {
        var filePaths = _presetExchangeDialog.ShowImportDialog();
        if (filePaths.Count == 0) return;

        ExecuteWithRetry(
            "Import presets",
            () =>
            {
                var importedPresets = _presetExchange.Import(filePaths);
                if (importedPresets.Count == 0)
                    return;

                var targetGroup = SelectedGroup;
                if (targetGroup is null || IsVirtualGroup(targetGroup))
                    targetGroup = Groups.FirstOrDefault(g => !IsVirtualGroup(g));
                if (targetGroup is null)
                    return;

                var stagedPresets = new List<EffectPreset>(importedPresets.Count);
                var stagedItems = new List<PresetItemViewModel>(importedPresets.Count);
                PresetItemViewModel? firstImported = null;
                try
                {
                    foreach (var preset in importedPresets)
                    {
                        if (string.IsNullOrWhiteSpace(preset.Name))
                            preset.Name = Texts.PresetManager_NewPreset;

                        _resilience.Execute($"Save imported preset: {preset.Id}", () => _persistence.SavePreset(preset), 3);
                        stagedPresets.Add(preset);

                        var vm = CreatePresetItem(preset);
                        stagedItems.Add(vm);
                        firstImported ??= vm;
                    }
                }
                catch
                {
                    foreach (var stagedPreset in stagedPresets)
                        _resilience.Execute($"Rollback imported preset: {stagedPreset.Id}", () => _persistence.DeletePreset(stagedPreset.Id), 2);
                    throw;
                }

                foreach (var vm in stagedItems)
                {
                    _presetCache[vm.Model.Id] = vm;
                    targetGroup.PresetIds.Add(vm.Model.Id);
                }

                PersistRegistry();
                SelectedGroup = targetGroup;
                RefreshDisplayedPresets();
                if (firstImported is not null)
                    SelectedPreset = firstImported;
            },
            Texts.PresetManager_ExchangeImportError,
            1);
    }

    private static string CreateSafeFileName(string source)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = string.Concat(source.Select(ch => invalidChars.Contains(ch) ? '_' : ch)).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? Texts.PresetManager_NewPreset : safeName;
    }

    private void ExecuteAddGroup()
    {
        var input = _dialog.ShowTextInput(Texts.Dialog_InputName, Texts.Dialog_Title, Texts.PresetManager_NewGroup);
        if (string.IsNullOrWhiteSpace(input)) return;

        var group = new PresetGroup { Name = input };
        Groups.Add(group);
        PersistRegistry();
        SelectedGroup = group;
    }

    private void ExecuteRenameGroup(PresetGroup? group)
    {
        var target = group ?? SelectedGroup;
        if (target is null || IsVirtualGroup(target)) return;

        var input = _dialog.ShowTextInput(Texts.Dialog_InputName, Texts.Dialog_Title, target.Name);
        if (string.IsNullOrWhiteSpace(input)) return;

        target.Name = input;
        PersistRegistry();
    }

    private void ExecuteRemoveGroup()
    {
        if (SelectedGroup is null || !CanRemoveGroup()) return;
        if (!_dialog.ShowConfirmation(Texts.Confirm_DeleteGroup, Texts.Confirm_Delete)) return;

        foreach (var id in SelectedGroup.PresetIds.ToList())
        {
            _historyWindowService.Close(id);
            PurgePreset(id);
        }
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

        var input = _dialog.ShowTextInput(Texts.Dialog_InputName, Texts.Dialog_Title, Texts.PresetManager_NewPreset);
        if (string.IsNullOrWhiteSpace(input)) return;

        var preset = new EffectPreset
        {
            Name = input,
            SerializedTabs = GetCurrentTabStateJson(),
            SerializedEffects = GetCurrentSelectedEffectsJson(),
        };
        var vm = CreatePresetItem(preset);
        _presetCache[preset.Id] = vm;
        targetGroup.PresetIds.Add(preset.Id);
        if (!ExecuteWithRetry($"Save new preset: {preset.Id}", () => _persistence.SavePreset(preset), maxAttempts: 3))
        {
            _presetCache.Remove(preset.Id);
            targetGroup.PresetIds.Remove(preset.Id);
            return;
        }
        PersistRegistry();
        SelectedGroup = targetGroup;
        RefreshDisplayedPresets();
        SelectedPreset = vm;
    }

    private void ExecuteRenamePreset(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? SelectedPreset;
        if (target is null) return;

        var input = _dialog.ShowTextInput(Texts.Dialog_InputName, Texts.Dialog_Title, target.Model.Name);
        if (string.IsNullOrWhiteSpace(input)) return;

        var previousName = target.Model.Name;
        target.Model.Name = input;
        target.RefreshName();
        if (!ExecuteWithRetry($"Rename preset: {target.Model.Id}", () => _persistence.SavePreset(target.Model), maxAttempts: 3))
        {
            target.Model.Name = previousName;
            target.RefreshName();
        }
    }

    private void ExecuteRemovePreset(IList? selectedItems) => RemoveTargets(selectedItems, false);

    private void RemoveTargets(IList? selectedItems, bool skipConfirm)
    {
        var targets = ResolveExportTargets(selectedItems);
        if (targets.Count == 0) return;

        if (!skipConfirm && !_dialog.ShowConfirmation(Texts.Confirm_DeletePreset, Texts.Confirm_Delete))
            return;

        var ids = targets.Select(t => t.Model.Id).ToList();
        foreach (var id in ids)
        {
            _historyWindowService.Close(id);
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

        var presetModels = presets.Select(p => p.Model).ToList();
        var isAppending = false;
        if (presetModels.Count == 1 && _effect.Effects.Count > 0)
        {
            if (_presetApplyPlanner.IsSameAsCurrentSelection(presetModels[0], _effect.Effects, Texts.EffectTab_FirstName))
            {
                if (!_dialog.ShowConfirmation(Texts.Confirm_SamePresetApply, Texts.Confirm_SamePresetApplyTitle))
                    return;
                isAppending = true;
            }
        }

        var plan = ExecuteWithRetry(
            "Create preset apply plan",
            () => _presetApplyPlanner.CreatePlan(presetModels, _effect.Effects, isAppending, Texts.EffectTab_FirstName),
            Texts.PresetManager_ApplyError,
            2);
        if (plan is null)
            return;

        var startedEdit = false;
        try
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            startedEdit = true;

            foreach (var prop in _itemProperties)
            {
                var target = (Effect.CombinedEffect)prop.PropertyOwner;
                target.EffectTabsJson = plan.EffectTabsJson;
                target.Effects = plan.Effects;
                target.SelectedPresetJson = plan.SelectedPresetJson;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Apply preset operation failed.", ex);
            _dialog.ShowMessage(Texts.PresetManager_ApplyError);
            return;
        }
        finally
        {
            if (startedEdit)
                EndEdit?.Invoke(this, EventArgs.Empty);
        }

        if (presetModels.Count == 1)
            _appliedPresetId = presetModels[0].Id;
        else
            _appliedPresetId = null;

        foreach (var preset in presetModels)
            _recentService.Add(preset.Id);

        if (SelectedGroup?.Name == Texts.PresetManager_GroupRecent)
            RefreshDisplayedPresets();

        TriggerUpdateCheck();
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

        _historyWindowService.Show(target.Model.Id, () =>
        {
            var vm = new HistoryManagerViewModel(target, _itemProperties);
            vm.BeginEdit += (_, _) => BeginEdit?.Invoke(this, EventArgs.Empty);
            vm.EndEdit += (_, _) => EndEdit?.Invoke(this, EventArgs.Empty);
            return vm;
        });

        CommandManager.InvalidateRequerySuggested();
    }

    private void ExecuteClearUnselected()
    {
        if (!_dialog.ShowConfirmation(Texts.Confirm_ClearUnselected, Texts.Confirm_ClearUnselectedTitle)) return;

        var currentEffects = _effect.Effects;
        var newEffects = currentEffects.Where(e => e.IsEnabled).ToList();
        if (currentEffects.Count == newEffects.Count) return;

        var startedEdit = false;
        try
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            startedEdit = true;

            var immutableEffects = ImmutableList.CreateRange(newEffects);
            foreach (var prop in _itemProperties)
            {
                var target = (Effect.CombinedEffect)prop.PropertyOwner;
                target.Effects = immutableEffects;
            }
        }
        finally
        {
            if (startedEdit)
                EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ExecuteClearPreset(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? SelectedPreset;
        if (target is null) return;

        if (!_dialog.ShowConfirmation(Texts.Confirm_ClearPreset, Texts.Confirm_ClearPresetTitle)) return;

        var emptyState = EffectTabStateService.CreateDefault(ImmutableList<IVideoEffect>.Empty, _serialization, Texts.EffectTab_FirstName);
        target.Model.SerializedTabs = EffectTabStateService.Serialize(emptyState);
        target.Model.SerializedEffects = EffectTabStateService.GetSelectedEffectsJson(emptyState);
        target.RefreshEffectInfo(_serialization);
        ExecuteWithRetry($"Clear preset: {target.Model.Id}", () => _persistence.SavePreset(target.Model), maxAttempts: 3);

        TriggerUpdateCheck();
    }

    private void ExecuteUpdatePreset(PresetItemViewModel? presetVm)
    {
        var target = presetVm ?? SelectedPreset;
        if (target is null) return;
        target.Model.SerializedTabs = GetCurrentTabStateJson();
        target.Model.SerializedEffects = GetCurrentSelectedEffectsJson();
        target.RefreshEffectInfo(_serialization);
        if (!ExecuteWithRetry($"Update preset: {target.Model.Id}", () => _persistence.SavePreset(target.Model), maxAttempts: 3))
            return;

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
        var state = EffectTabStateService.ResolveEffectState(_effect.EffectTabsJson, _effect.Effects, _serialization, Texts.EffectTab_FirstName);
        var selectedTab = EffectTabStateService.GetSelectedTab(state);
        selectedTab.SerializedEffects = _serialization.Serialize(_effect.Effects);
        return state;
    }

    private string GetCurrentTabStateJson() =>
        EffectTabStateService.Serialize(GetCurrentTabState());

    private string GetCurrentSelectedEffectsJson() =>
        EffectTabStateService.GetSelectedEffectsJson(GetCurrentTabState());

    private void ExecuteToggleFavorite(PresetItemViewModel? presetVm)
    {
        if (presetVm is null) return;
        var previousFavorite = presetVm.Model.IsFavorite;
        presetVm.Model.IsFavorite = !presetVm.Model.IsFavorite;
        presetVm.RefreshFavorite();
        if (!ExecuteWithRetry($"Toggle favorite preset: {presetVm.Model.Id}", () => _persistence.SavePreset(presetVm.Model), maxAttempts: 3))
        {
            presetVm.Model.IsFavorite = previousFavorite;
            presetVm.RefreshFavorite();
            return;
        }
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

    private PresetItemViewModel CreatePresetItem(EffectPreset preset) =>
        new(preset, _serialization, _logger);

    private void PurgePreset(Guid id)
    {
        _presetCache.Remove(id);
        ExecuteWithRetry($"Delete preset: {id}", () => _persistence.DeletePreset(id), maxAttempts: 3);
        _recentService.Remove(id);
    }

    private void PersistRegistry()
    {
        var registry = new GroupRegistry
        {
            Groups = [.. Groups.Where(g => !IsVirtualGroup(g))]
        };
        ExecuteWithRetry("Save group registry", () => _persistence.SaveGroupRegistry(registry), maxAttempts: 3);
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
        InputManager.Current.PostProcessInput -= OnPostProcessInput;
        ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
        _pendingInputDrivenCheckOperation = null;
        _hasPotentialUnnotifiedEffectMutation = false;
        Volatile.Write(ref _lastEvaluatedSnapshot, null);
        _historyWindowService.CloseAll();
        _effect.PropertyChanged -= OnEffectPropertyChanged;
        ResourceRegistry.Instance.Unregister(this);
    }
}