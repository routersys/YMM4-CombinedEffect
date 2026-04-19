using CombinedEffect.ViewModels;

namespace CombinedEffect.Services.Interfaces;

internal interface IHistoryWindowService : IDisposable
{
    void Show(Guid presetId, Func<HistoryManagerViewModel> createViewModel);
    void Close(Guid presetId);
    void CloseAll();
}