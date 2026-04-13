using CombinedEffect.Services;
using CombinedEffect.ViewModels;
using System.Windows;

namespace CombinedEffect.Views;

public partial class HistoryManagerWindow : Window
{
    internal HistoryManagerWindow(HistoryManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ServiceRegistry.Instance.WindowTheme.Bind(this);
    }
}