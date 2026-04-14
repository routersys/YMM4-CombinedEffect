using CombinedEffect.Localization;
using CombinedEffect.Models;
using CombinedEffect.Services.Interfaces;
using Microsoft.Win32;

namespace CombinedEffect.Services;

internal sealed class PresetExchangeDialogService : IPresetExchangeDialogService
{
    public string? ShowExportDialog(string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = PresetExchangeFormat.Extension.TrimStart('.'),
            Filter = BuildFilter(),
            FileName = string.IsNullOrWhiteSpace(defaultFileName) ? Texts.PresetManager_NewPreset : defaultFileName,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public IReadOnlyList<string> ShowImportDialog()
    {
        var dialog = new OpenFileDialog
        {
            Filter = BuildFilter(),
            Multiselect = true,
            CheckFileExists = true,
            CheckPathExists = true,
        };

        return dialog.ShowDialog() == true
            ? dialog.FileNames
            : [];
    }

    private static string BuildFilter() =>
        $"{Texts.PresetManager_ExchangeFileType} (*{PresetExchangeFormat.Extension})|*{PresetExchangeFormat.Extension}|{Texts.PresetManager_AllFiles} (*.*)|*.*";
}