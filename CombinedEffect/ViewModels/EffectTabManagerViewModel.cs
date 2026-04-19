using CombinedEffect.Infrastructure;
using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services;
using CombinedEffect.Services.Interfaces;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace CombinedEffect.ViewModels;

internal sealed class EffectTabManagerViewModel(ItemProperty[] itemProperties) : ObservableBase, IDisposable
{
    private readonly ItemProperty[] _itemProperties = itemProperties;
    private readonly IEffectSerializationService _serialization = ServiceRegistry.Instance.EffectSerialization;
    private readonly Effect.CombinedEffect? _effect = itemProperties.Length > 0 ? (Effect.CombinedEffect)itemProperties[0].PropertyOwner : null;

    private bool _disposed;
    private bool _isInternalSync;
    private bool _isLoadingState;

    public ObservableCollection<EffectTabItemViewModel> Tabs { get; } = [];

    public EffectTabItemViewModel? SelectedTab
    {
        get;
        set
        {
            var hadCommittedEdits = CommitOtherEditingTabs(value);
            if (!SetProperty(ref field, value)) return;
            if (_isLoadingState) return;
            if (hadCommittedEdits)
                PersistTabState(applyEffects: false, raiseEdit: false);
            ApplySelectedTabEffects(raiseEdit: true);
            PersistTabState(applyEffects: false, raiseEdit: false);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand AddTabCommand { get; private set; } = null!;
    public ICommand RemoveTabCommand { get; private set; } = null!;
    public ICommand BeginRenameTabCommand { get; private set; } = null!;
    public ICommand CommitRenameTabCommand { get; private set; } = null!;
    public ICommand CancelRenameTabCommand { get; private set; } = null!;

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;
    public event Action<Guid>? RenameRequested;

    public EffectTabManagerViewModel() : this([]) { }

    public void Initialize()
    {
        AddTabCommand = new RelayCommand<object>(_ => ExecuteAddTab());
        RemoveTabCommand = new RelayCommand<EffectTabItemViewModel>(ExecuteRemoveTab, CanRemoveTab);
        BeginRenameTabCommand = new RelayCommand<EffectTabItemViewModel>(ExecuteBeginRenameTab, tab => tab is not null);
        CommitRenameTabCommand = new RelayCommand<EffectTabItemViewModel>(ExecuteCommitRenameTab, tab => tab is not null);
        CancelRenameTabCommand = new RelayCommand<EffectTabItemViewModel>(ExecuteCancelRenameTab, tab => tab is not null);

        if (_effect is not null)
            _effect.PropertyChanged += OnEffectPropertyChanged;

        LoadStateFromEffect();
        ResourceRegistry.Instance.Register(this);
    }

    private bool CanRemoveTab(EffectTabItemViewModel? tab)
    {
        var target = tab ?? SelectedTab;
        return target is not null && !target.IsFirstTab;
    }

    private void ExecuteAddTab()
    {
        if (_effect is null) return;

        var newTab = new EffectTabItemViewModel(new EffectTab
        {
            Name = CreateTabName(Tabs.Count),
            SerializedEffects = _serialization.Serialize(ImmutableList<IVideoEffect>.Empty)
        });

        Tabs.Add(newTab);
        ReindexTabs();
        SelectedTab = newTab;
        PersistTabState(applyEffects: true, raiseEdit: true);
        ExecuteBeginRenameTab(newTab);
    }

    private void ExecuteRemoveTab(EffectTabItemViewModel? tab)
    {
        var target = tab ?? SelectedTab;
        if (target is null || target.IsFirstTab) return;

        var removeIndex = Tabs.IndexOf(target);
        if (removeIndex < 0) return;

        var nextSelectionIndex = Math.Max(0, removeIndex - 1);

        Tabs.RemoveAt(removeIndex);
        ReindexTabs();

        if (Tabs.Count == 0)
        {
            LoadStateFromEffect();
            return;
        }

        _isLoadingState = true;
        SelectedTab = Tabs[nextSelectionIndex];
        _isLoadingState = false;

        ApplySelectedTabEffects(raiseEdit: true);
        PersistTabState(applyEffects: false, raiseEdit: false);
    }

    private void ExecuteBeginRenameTab(EffectTabItemViewModel? tab)
    {
        if (tab is null) return;
        tab.BeginEdit();
        RenameRequested?.Invoke(tab.Id);
    }

    private void ExecuteCommitRenameTab(EffectTabItemViewModel? tab)
    {
        if (tab is null) return;
        tab.CommitEdit(CreateTabName(tab.Index));
        PersistTabState(applyEffects: false, raiseEdit: false);
    }

    private void ExecuteCancelRenameTab(EffectTabItemViewModel? tab)
    {
        if (tab is null) return;
        tab.CancelEdit();
    }

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_effect is null || _isInternalSync || _disposed) return;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_disposed || _effect is null) return;

            if (e.PropertyName == nameof(Effect.CombinedEffect.Effects))
            {
                if (SelectedTab is null) return;
                SelectedTab.SerializedEffects = _serialization.Serialize(_effect.Effects);
                PersistTabState(applyEffects: false, raiseEdit: false);
            }
            else if (e.PropertyName == nameof(Effect.CombinedEffect.EffectTabsJson))
            {
                LoadStateFromEffect();
            }
        });
    }

    private void LoadStateFromEffect()
    {
        if (_effect is null)
        {
            Tabs.Clear();
            SelectedTab = null;
            return;
        }

        EffectTabState? parsed = null;
        if (EffectTabStateService.TryDeserialize(_effect.EffectTabsJson, out var state))
            parsed = state;

        var normalized = EffectTabStateService.Normalize(parsed, _effect.Effects, _serialization, Texts.EffectTab_FirstName);

        _isLoadingState = true;
        Tabs.Clear();
        foreach (var model in normalized.Tabs)
            Tabs.Add(new EffectTabItemViewModel(model));
        ReindexTabs();

        SelectedTab = Tabs.FirstOrDefault(t => t.Id == normalized.SelectedTabId) ?? Tabs.FirstOrDefault();
        _isLoadingState = false;

        PersistTabState(applyEffects: true, raiseEdit: false);
    }

    private void ApplySelectedTabEffects(bool raiseEdit)
    {
        if (_effect is null || SelectedTab is null) return;

        var effects = _serialization.Deserialize(SelectedTab.SerializedEffects) ?? ImmutableList<IVideoEffect>.Empty;
        ApplyToOwners(effects, raiseEdit);
    }

    private void ApplyToOwners(ImmutableList<IVideoEffect> effects, bool raiseEdit)
    {
        if (_effect is null) return;

        _isInternalSync = true;
        try
        {
            if (raiseEdit)
                BeginEdit?.Invoke(this, EventArgs.Empty);

            foreach (var prop in _itemProperties)
            {
                var target = (Effect.CombinedEffect)prop.PropertyOwner;
                target.Effects = effects;
            }

            if (raiseEdit)
                EndEdit?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isInternalSync = false;
        }
    }

    private void PersistTabState(bool applyEffects, bool raiseEdit)
    {
        if (_effect is null || Tabs.Count == 0) return;

        var state = new EffectTabState
        {
            SelectedTabId = SelectedTab?.Id ?? Tabs[0].Id,
            Tabs = [.. Tabs.Select(t => new EffectTab
            {
                Id = t.Id,
                Name = t.Name,
                SerializedEffects = t.SerializedEffects,
            })]
        };

        var json = EffectTabStateService.Serialize(state);

        _isInternalSync = true;
        try
        {
            if (applyEffects)
            {
                var effects = SelectedTab is null
                    ? ImmutableList<IVideoEffect>.Empty
                    : _serialization.Deserialize(SelectedTab.SerializedEffects) ?? ImmutableList<IVideoEffect>.Empty;

                if (raiseEdit)
                    BeginEdit?.Invoke(this, EventArgs.Empty);

                foreach (var prop in _itemProperties)
                {
                    var target = (Effect.CombinedEffect)prop.PropertyOwner;
                    target.EffectTabsJson = json;
                    target.Effects = effects;
                }

                if (raiseEdit)
                    EndEdit?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                foreach (var prop in _itemProperties)
                {
                    var target = (Effect.CombinedEffect)prop.PropertyOwner;
                    target.EffectTabsJson = json;
                }
            }
        }
        finally
        {
            _isInternalSync = false;
        }
    }

    private void ReindexTabs()
    {
        for (var i = 0; i < Tabs.Count; i++)
            Tabs[i].Index = i;
    }

    private bool CommitOtherEditingTabs(EffectTabItemViewModel? keepEditingTab)
    {
        var committed = false;
        foreach (var tab in Tabs)
        {
            if (!tab.IsEditing) continue;
            if (tab == keepEditingTab) continue;
            tab.CommitEdit(CreateTabName(tab.Index));
            committed = true;
        }
        return committed;
    }

    private static string CreateTabName(int index) =>
        index == 0 ? Texts.EffectTab_FirstName : string.Format(Texts.EffectTab_NumberedName, index + 1);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_effect is not null)
            _effect.PropertyChanged -= OnEffectPropertyChanged;

        ResourceRegistry.Instance.Unregister(this);
    }
}