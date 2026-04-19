namespace CombinedEffect.Services.Interfaces;

internal interface IUserDialogService
{
    string? ShowTextInput(string message, string title, string defaultText = "");
    bool ShowConfirmation(string message, string title);
    void ShowMessage(string message);
}