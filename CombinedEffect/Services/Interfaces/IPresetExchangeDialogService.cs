namespace CombinedEffect.Services.Interfaces;

internal interface IPresetExchangeDialogService
{
    string? ShowExportDialog(string defaultFileName);
    IReadOnlyList<string> ShowImportDialog();
}