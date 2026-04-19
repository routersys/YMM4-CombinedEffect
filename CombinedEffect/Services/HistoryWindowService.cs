using CombinedEffect.Services.Interfaces;
using CombinedEffect.ViewModels;
using CombinedEffect.Views;
using System.Windows;

namespace CombinedEffect.Services;

internal sealed class HistoryWindowService : IHistoryWindowService
{
    private readonly Dictionary<Guid, (Window Window, HistoryManagerViewModel ViewModel)> _windows = new();
    private bool _disposed;

    public void Show(Guid presetId, Func<HistoryManagerViewModel> createViewModel)
    {
        if (_disposed)
            return;

        if (_windows.TryGetValue(presetId, out var existing))
        {
            if (existing.Window.IsVisible)
            {
                existing.Window.Focus();
                return;
            }

            _windows.Remove(presetId);
            existing.ViewModel.Dispose();
        }

        var viewModel = createViewModel();
        var window = new HistoryManagerWindow(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        window.Closed += (_, _) =>
        {
            if (_windows.Remove(presetId, out var tuple))
                tuple.ViewModel.Dispose();
        };

        _windows[presetId] = (window, viewModel);
        window.Show();
    }

    public void Close(Guid presetId)
    {
        if (_windows.TryGetValue(presetId, out var existing))
            existing.Window.Close();
    }

    public void CloseAll()
    {
        foreach (var (_, tuple) in _windows.ToArray())
            tuple.Window.Close();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        CloseAll();
        _windows.Clear();
    }
}