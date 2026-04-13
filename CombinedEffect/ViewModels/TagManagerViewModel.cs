using CombinedEffect.Infrastructure;
using CombinedEffect.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CombinedEffect.ViewModels;

internal sealed class TagManagerViewModel(Guid presetId, HistorySnapshotViewModel snapshotVm, IHistoryRepository repository) : ObservableBase
{
    public ObservableCollection<string> Tags { get; } = [.. snapshotVm.Model.Tags];

    public string NewTag
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public ICommand AddTagCommand
    {
        get => field ??= new RelayCommand<object>(_ => ExecuteAddTag(), _ => !string.IsNullOrWhiteSpace(NewTag));
    }

    public ICommand RemoveTagCommand
    {
        get => field ??= new RelayCommand<string>(ExecuteRemoveTag);
    }

    private void ExecuteAddTag()
    {
        var tag = NewTag.Trim();
        if (!string.IsNullOrWhiteSpace(tag) && !Tags.Contains(tag))
        {
            Tags.Add(tag);
            snapshotVm.Model.Tags.Add(tag);
            repository.SaveSnapshot(presetId, snapshotVm.Model);
            snapshotVm.RefreshTags();
            NewTag = string.Empty;
        }
    }

    private void ExecuteRemoveTag(string? tag)
    {
        if (tag != null && Tags.Remove(tag))
        {
            snapshotVm.Model.Tags.Remove(tag);
            repository.SaveSnapshot(presetId, snapshotVm.Model);
            snapshotVm.RefreshTags();
        }
    }
}