using CombinedEffect.Infrastructure;
using CombinedEffect.Models;
using System.Globalization;

namespace CombinedEffect.ViewModels;

internal sealed class EffectTabItemViewModel : ObservableBase
{
    public EffectTab Model { get; }

    public EffectTabItemViewModel(EffectTab model)
    {
        Model = model;
        _editName = model.Name;
    }

    public Guid Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name == value) return;
            Model.Name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CompactLabel));
        }
    }

    public string SerializedEffects
    {
        get => Model.SerializedEffects;
        set
        {
            if (Model.SerializedEffects == value) return;
            Model.SerializedEffects = value;
            OnPropertyChanged();
        }
    }

    public int Index
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(IsFirstTab));
            OnPropertyChanged(nameof(IndexLabel));
            OnPropertyChanged(nameof(CompactLabel));
        }
    }

    public bool IsFirstTab => Index == 0;

    public string IndexLabel => (Index + 1).ToString(CultureInfo.InvariantCulture);

    public string CompactLabel
    {
        get
        {
            var trimmed = Name?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                return StringInfo.GetNextTextElement(trimmed, 0);
            return IndexLabel;
        }
    }

    public bool IsEditing
    {
        get;
        set => SetProperty(ref field, value);
    }

    private string _editName;
    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public void BeginEdit()
    {
        EditName = Name;
        IsEditing = true;
    }

    public void CommitEdit(string fallbackName)
    {
        var next = string.IsNullOrWhiteSpace(EditName) ? fallbackName : EditName.Trim();
        Name = next;
        EditName = next;
        IsEditing = false;
    }

    public void CancelEdit()
    {
        EditName = Name;
        IsEditing = false;
    }
}