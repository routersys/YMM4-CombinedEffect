using CombinedEffect.Infrastructure;
using CombinedEffect.Models;
using CombinedEffect.Models.History;
using CombinedEffect.Services;
using CombinedEffect.Services.Interfaces;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace CombinedEffect.ViewModels;

internal sealed class HistoryManagerViewModel : ObservableBase, IDisposable
{
    private readonly IHistoryRepository _repository = ServiceRegistry.Instance.HistoryRepository;
    private readonly IEffectSerializationService _serialization = ServiceRegistry.Instance.EffectSerialization;
    private readonly IPresetPersistenceService _persistence = ServiceRegistry.Instance.PresetPersistence;
    private readonly PresetItemViewModel _presetVm;
    private readonly ItemProperty[] _itemProperties;
    private readonly Effect.CombinedEffect _effect;
    private readonly Guid _presetId;

    private volatile bool _disposed;

    public HistoryManagerViewModel(PresetItemViewModel presetVm, ItemProperty[] itemProperties)
    {
        _presetVm = presetVm;
        _itemProperties = itemProperties;
        _effect = (Effect.CombinedEffect)itemProperties[0].PropertyOwner;
        _presetId = presetVm.Model.Id;
        CurrentRawData = _serialization.Serialize(_effect.Effects);
        AttachAndLoad();
    }

    public string WindowTitle => $"{Localization.Texts.History_Title} - {_presetVm.Model.Name}";

    public ObservableCollection<HistoryBranch> Branches { get; } = [];
    public ObservableCollection<HistorySnapshotViewModel> Snapshots { get; } = [];

    public HistoryBranch? SelectedBranch
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            RefreshSnapshots();
        }
    }

    public HistorySnapshotViewModel? SelectedSnapshot
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            UpdateDiffs();
        }
    }

    public string CurrentRawData
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public ObservableCollection<DiffNode> ParameterDiffList { get; } = [];

    public string SnapshotMessage
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string NewBranchName
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public ICommand CreateSnapshotCommand { get => field ??= new RelayCommand<object>(_ => ExecuteCreateSnapshot(), _ => !string.IsNullOrWhiteSpace(SnapshotMessage)); }
    public ICommand CreateBranchCommand { get => field ??= new RelayCommand<object>(_ => ExecuteCreateBranch(), _ => !string.IsNullOrWhiteSpace(NewBranchName)); }
    public ICommand RevertSnapshotCommand { get => field ??= new RelayCommand<object>(_ => ExecuteRevertSnapshot(), _ => SelectedSnapshot is not null); }
    public ICommand MergeSnapshotCommand { get => field ??= new RelayCommand<HistorySnapshotViewModel>(ExecuteMerge); }
    public ICommand ResetSoftCommand { get => field ??= new RelayCommand<HistorySnapshotViewModel>(ExecuteResetSoft); }
    public ICommand ResetHardCommand { get => field ??= new RelayCommand<HistorySnapshotViewModel>(ExecuteResetHard); }
    public ICommand ManageTagsCommand { get => field ??= new RelayCommand<HistorySnapshotViewModel>(ExecuteManageTags); }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    private void AttachAndLoad()
    {
        _presetVm.PropertyChanged += OnPresetPropertyChanged;
        _effect.PropertyChanged += OnEffectPropertyChanged;
        ResourceRegistry.Instance.Register(this);
        _ = LoadDataAsync();
    }

    private void OnPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PresetItemViewModel.Name))
            OnPropertyChanged(nameof(WindowTitle));
    }

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Effect.CombinedEffect.Effects)) return;
        var serialized = _serialization.Serialize(_effect.Effects);
        if (CurrentRawData == serialized) return;
        CurrentRawData = serialized;
        UpdateDiffs();
    }

    private async Task LoadDataAsync()
    {
        var branches = await _repository.LoadBranchesAsync(_presetId).ConfigureAwait(false);
        if (branches.Count == 0)
        {
            var defaultBranch = new HistoryBranch { Name = Localization.Texts.History_DefaultBranchName };
            branches.Add(defaultBranch);
            _repository.SaveBranches(_presetId, branches);
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Branches.Clear();
            foreach (var b in branches) Branches.Add(b);
            SelectedBranch = Branches.FirstOrDefault();
        });
    }

    private async void RefreshSnapshots()
    {
        try
        {
            var branch = SelectedBranch;
            if (branch is null)
            {
                await Application.Current.Dispatcher.InvokeAsync(Snapshots.Clear);
                return;
            }

            var snaps = (await _repository.LoadAllSnapshotsAsync(_presetId).ConfigureAwait(false)).ToDictionary(s => s.Id);
            if (_disposed) return;
            var currentJson = _presetVm.Model.SerializedEffects;

            var currentId = branch.HeadSnapshotId;
            var newSnapshots = new List<HistorySnapshotViewModel>();
            while (currentId != Guid.Empty && snaps.TryGetValue(currentId, out var s))
            {
                var vm = new HistorySnapshotViewModel(s) { IsCurrent = s.Id == branch.HeadSnapshotId };
                if (s.SerializedEffects != currentJson)
                    vm.DiffSummary = CalculateDiffSummary(currentJson, s.SerializedEffects);
                newSnapshots.Add(vm);
                currentId = s.ParentId;
            }

            if (_disposed) return;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Snapshots.Clear();
                foreach (var vm in newSnapshots)
                    Snapshots.Add(vm);
            });
        }
        catch { }
    }

    private string CalculateDiffSummary(string currentJson, string snapshotJson)
    {
        var currentEffects = _serialization.Deserialize(currentJson) ?? [];
        var snapEffects = _serialization.Deserialize(snapshotJson) ?? [];
        int countDiff = snapEffects.Count - currentEffects.Count;
        if (countDiff == 0) return Localization.Texts.History_Modified;
        if (countDiff > 0) return $"{Localization.Texts.History_Added} (+{countDiff})";
        return $"{Localization.Texts.History_Removed} ({Math.Abs(countDiff)})";
    }

    private void UpdateDiffs()
    {
        ParameterDiffList.Clear();
        var snapJson = SelectedSnapshot?.SerializedEffects;
        if (string.IsNullOrEmpty(snapJson) || string.IsNullOrEmpty(CurrentRawData)) return;

        try
        {
            var nodes = GenerateParameterDiffs(snapJson, CurrentRawData);
            foreach (var node in nodes) ParameterDiffList.Add(node);
        }
        catch (Exception ex) when (ex is Newtonsoft.Json.JsonException or InvalidOperationException)
        {
            ParameterDiffList.Add(new DiffNode(Localization.Texts.History_DiffExtractionFailed, DiffType.Modified));
        }
    }

    private List<DiffNode> GenerateParameterDiffs(string oldJson, string newJson)
    {
        var oldArray = JArray.Parse(oldJson);
        var newArray = JArray.Parse(newJson);
        var list = new List<DiffNode>();

        var adds = newArray.Count - oldArray.Count;
        var countDiffText = string.Format(Localization.Texts.History_EffectsCountDiff, $"{adds:+#;-#;0}");
        list.Add(new DiffNode(countDiffText, adds > 0 ? DiffType.Added : adds < 0 ? DiffType.Removed : DiffType.Unchanged));

        for (int i = 0; i < Math.Min(oldArray.Count, newArray.Count); i++)
        {
            var oldObj = oldArray[i] as JObject;
            var newObj = newArray[i] as JObject;
            if (oldObj is null || newObj is null) continue;

            string objType = oldObj["$type"]?.ToString() ?? $"Effect[{i}]";
            var typeName = objType.Split(',')[0].Split('.').LastOrDefault() ?? objType;
            bool addedTitle = false;

            foreach (var prop in newObj.Properties())
            {
                var oldProp = oldObj[prop.Name];
                if (oldProp == null) continue;
                if (!JToken.DeepEquals(prop.Value, oldProp))
                {
                    if (!addedTitle)
                    {
                        list.Add(new DiffNode(string.Format(Localization.Texts.History_DiffEffectHeader, typeName), DiffType.Unchanged));
                        addedTitle = true;
                    }
                    list.Add(new DiffNode(string.Format(Localization.Texts.History_DiffPropertyChange, prop.Name, oldProp, prop.Value), DiffType.Modified));
                }
            }
        }
        return list;
    }

    private void ExecuteCreateSnapshot()
    {
        if (SelectedBranch is null) return;
        var snapshot = new HistorySnapshot
        {
            ParentId = SelectedBranch.HeadSnapshotId,
            Message = SnapshotMessage,
            SerializedEffects = _serialization.Serialize(_effect.Effects)
        };
        _repository.SaveSnapshot(_presetId, snapshot);

        SelectedBranch.HeadSnapshotId = snapshot.Id;
        _repository.SaveBranches(_presetId, [.. Branches]);

        SnapshotMessage = string.Empty;
        RefreshSnapshots();
    }

    private void ExecuteCreateBranch()
    {
        var branch = new HistoryBranch
        {
            Name = NewBranchName,
            HeadSnapshotId = SelectedSnapshot?.Id ?? SelectedBranch?.HeadSnapshotId ?? Guid.Empty
        };
        Branches.Add(branch);
        _repository.SaveBranches(_presetId, [.. Branches]);
        NewBranchName = string.Empty;
        SelectedBranch = branch;
    }

    private void ExecuteRevertSnapshot()
    {
        if (SelectedSnapshot is null) return;
        ExecuteRevertSnapshotCore(SelectedSnapshot);
    }

    private void ExecuteRevertSnapshotCore(HistorySnapshotViewModel? vm)
    {
        if (vm is null) return;
        var effects = _serialization.Deserialize(vm.SerializedEffects);
        if (effects is null) return;

        _presetVm.Model.SerializedEffects = vm.SerializedEffects;
        _presetVm.RefreshEffectInfo(_serialization);
        _persistence.SavePreset(_presetVm.Model);

        BeginEdit?.Invoke(this, EventArgs.Empty);
        foreach (var prop in _itemProperties)
        {
            var target = (Effect.CombinedEffect)prop.PropertyOwner;
            target.Effects = ImmutableList.CreateRange(effects);
        }
        EndEdit?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteMerge(HistorySnapshotViewModel? vm)
    {
        if (vm is null) return;
        var targetEffects = _serialization.Deserialize(vm.SerializedEffects);
        if (targetEffects == null) return;
        var combined = _effect.Effects.AddRange(targetEffects);
        _presetVm.Model.SerializedEffects = _serialization.Serialize(combined);
        _presetVm.RefreshEffectInfo(_serialization);
        _persistence.SavePreset(_presetVm.Model);

        BeginEdit?.Invoke(this, EventArgs.Empty);
        foreach (var prop in _itemProperties)
        {
            var target = (Effect.CombinedEffect)prop.PropertyOwner;
            target.Effects = combined;
        }
        EndEdit?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteResetSoft(HistorySnapshotViewModel? vm)
    {
        if (SelectedBranch is null || vm is null) return;
        SelectedBranch.HeadSnapshotId = vm.Id;
        _repository.SaveBranches(_presetId, [.. Branches]);
        RefreshSnapshots();
    }

    private void ExecuteResetHard(HistorySnapshotViewModel? vm)
    {
        if (vm is null) return;
        ExecuteResetSoft(vm);
        ExecuteRevertSnapshotCore(vm);
    }

    private void ExecuteManageTags(HistorySnapshotViewModel? vm)
    {
        if (vm is null) return;
        var tagVm = new TagManagerViewModel(_presetId, vm, _repository);
        var window = new Views.TagManagerWindow(tagVm) { Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _presetVm.PropertyChanged -= OnPresetPropertyChanged;
        _effect.PropertyChanged -= OnEffectPropertyChanged;
        ResourceRegistry.Instance.Unregister(this);
    }
}