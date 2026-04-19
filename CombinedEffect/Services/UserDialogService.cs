using CombinedEffect.Services.Interfaces;
using CombinedEffect.Views;
using System.Windows;

namespace CombinedEffect.Services;

internal sealed class UserDialogService : IUserDialogService
{
    public string? ShowTextInput(string message, string title, string defaultText = "")
    {
        var inputWindow = new InputDialogWindow(message, title, defaultText)
        {
            Owner = Application.Current.MainWindow,
        };

        return inputWindow.ShowDialog() == true
            ? inputWindow.InputText
            : null;
    }

    public bool ShowConfirmation(string message, string title)
    {
        var confirmWindow = new ConfirmationDialogWindow(message, title)
        {
            Owner = Application.Current.MainWindow,
        };

        return confirmWindow.ShowDialog() == true;
    }

    public void ShowMessage(string message)
    {
        MessageBox.Show(message);
    }
}