using CombinedEffect.Services;
using CombinedEffect.ViewModels;
using System.Windows;

namespace CombinedEffect.Views;

public partial class ConfirmationDialogWindow : Window
{
    public ConfirmationDialogWindow(string message, string title)
    {
        InitializeComponent();
        DataContext = new ConfirmationDialogViewModel(message, title);
        ServiceRegistry.Instance.WindowTheme.Bind(this);
    }

    private void YesButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void NoButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}