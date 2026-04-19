using CombinedEffect.ViewModels;
using CombinedEffect.Views;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace CombinedEffect.Attributes;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class EffectTabManagerControlAttribute : PropertyEditorAttribute2
{
    public EffectTabManagerControlAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create() => new EffectTabManagerControl();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not EffectTabManagerControl editor) return;
        var vm = new EffectTabManagerViewModel(itemProperties);
        vm.Initialize();
        editor.DataContext = vm;
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is not EffectTabManagerControl editor) return;
        if (editor.DataContext is EffectTabManagerViewModel vm) vm.Dispose();
        editor.DataContext = null;
    }
}