using CombinedEffect.Infrastructure;
using CombinedEffect.Models.History;

namespace CombinedEffect.ViewModels;

internal sealed class HistorySnapshotViewModel : ObservableBase
{
    private readonly HistorySnapshot _model;

    public HistorySnapshot Model => _model;

    public Guid Id => _model.Id;
    public DateTime Timestamp => _model.Timestamp;
    public string Message => _model.Message;
    public string SerializedEffects => _model.SerializedEffects;

    public string DisplayTags => _model.Tags.Count > 0 ? $"[{string.Join(", ", _model.Tags)}]" : string.Empty;

    public void RefreshTags() => OnPropertyChanged(nameof(DisplayTags));

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    private string _diffSummary = string.Empty;
    public string DiffSummary
    {
        get => _diffSummary;
        set => SetProperty(ref _diffSummary, value);
    }

    public HistorySnapshotViewModel(HistorySnapshot model)
    {
        _model = model;
    }
}