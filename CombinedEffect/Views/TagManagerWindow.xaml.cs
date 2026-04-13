using CombinedEffect.Services;
using CombinedEffect.ViewModels;
using System.Windows;

namespace CombinedEffect.Views;

public partial class TagManagerWindow : Window
{
    internal TagManagerWindow(TagManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ServiceRegistry.Instance.WindowTheme.Bind(this);
    }
}