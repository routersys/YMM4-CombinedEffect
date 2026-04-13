using CombinedEffect.Services;
using CombinedEffect.ViewModels;
using System.Windows;

namespace CombinedEffect.Views;

public partial class InputDialogWindow : Window
{
    public string InputText => ((InputDialogViewModel)DataContext).InputText;

    public InputDialogWindow(string message, string title, string defaultText = "")
    {
        InitializeComponent();
        DataContext = new InputDialogViewModel(message, title, defaultText);
        ServiceRegistry.Instance.WindowTheme.Bind(this);
        InputTextBox.Focus();
        InputTextBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}