using System.Windows;
using CombinedEffect.ViewModels;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace CombinedEffect.Views
{
    internal class PresetManagerControlAttribute : PropertyEditorAttribute2
    {
        public PresetManagerControlAttribute()
        {
            PropertyEditorSize = PropertyEditorSize.FullWidth;
        }

        public override FrameworkElement Create()
        {
            return new PresetManagerControl();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not PresetManagerControl editor)
                return;

            editor.DataContext = new PresetManagerViewModel(itemProperties);
        }
        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not PresetManagerControl editor)
                return;

            editor.DataContext = null;
        }
    }
}