using CombinedEffect.Infrastructure;

namespace CombinedEffect.ViewModels;

internal sealed class InputDialogViewModel : ObservableBase
{
    private string _inputText = string.Empty;

    public string Message { get; }
    public string Title { get; }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public InputDialogViewModel(string message, string title, string defaultText = "")
    {
        Message = message;
        Title = title;
        _inputText = defaultText;
    }
}