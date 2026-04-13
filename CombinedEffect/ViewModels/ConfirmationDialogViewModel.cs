namespace CombinedEffect.ViewModels;

internal sealed class ConfirmationDialogViewModel
{
    public string Message { get; }
    public string Title { get; }

    public ConfirmationDialogViewModel(string message, string title)
    {
        Message = message;
        Title = title;
    }
}