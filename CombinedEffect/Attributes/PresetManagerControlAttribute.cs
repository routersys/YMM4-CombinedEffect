using CombinedEffect.ViewModels;
using CombinedEffect.Views;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace CombinedEffect.Attributes;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class PresetManagerControlAttribute : PropertyEditorAttribute2
{
    public PresetManagerControlAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create() => new PresetManagerControl();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not PresetManagerControl editor) return;
        editor.DataContext = new PresetManagerViewModel(itemProperties);
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is not PresetManagerControl editor) return;
        if (editor.DataContext is PresetManagerViewModel vm) vm.Dispose();
        editor.DataContext = null;
    }
}
