using CombinedEffect.Models;
using CombinedEffect.ViewModels;
using CombinedEffect.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using YukkuriMovieMaker.Commons;

namespace CombinedEffect.Views;

public partial class PresetManagerControl : UserControl, IPropertyEditorControl
{
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    private Point _dragStartPoint;
    private DropInsertionAdorner? _insertionAdorner;
    private AdornerLayer? _adornerLayer;

    public PresetManagerControl()
    {
        InitializeComponent();
        DataContextChanged += PresetManagerControl_DataContextChanged;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = ServiceRegistry.Instance.UISettings.Settings;
        Height = Math.Max(200, settings.ControlHeight);
        GroupColumn.Width = new GridLength(Math.Max(50, settings.GroupColumnWidth));
    }

    private void PresetManagerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PresetManagerViewModel oldVm)
        {
            oldVm.BeginEdit -= OnBeginEdit;
            oldVm.EndEdit -= OnEndEdit;
        }
        if (e.NewValue is PresetManagerViewModel newVm)
        {
            newVm.BeginEdit += OnBeginEdit;
            newVm.EndEdit += OnEndEdit;
        }
    }

    private void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);
    private void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

    private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var newHeight = ActualHeight + e.VerticalChange;
        if (newHeight >= MinHeight)
        {
            Height = newHeight;
            var uiSettings = ServiceRegistry.Instance.UISettings;
            uiSettings.Settings.ControlHeight = newHeight;
            uiSettings.Save();
        }
    }

    private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        var uiSettings = ServiceRegistry.Instance.UISettings;
        uiSettings.Settings.GroupColumnWidth = GroupColumn.Width.Value;
        uiSettings.Save();
    }

    private void RootControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width < 400)
        {
            MobileMenuButton.Visibility = Visibility.Visible;
            GroupPanel.Visibility = Visibility.Collapsed;
            GroupSplitter.Visibility = Visibility.Collapsed;
            GroupColumn.MinWidth = 0;
            GroupColumn.MaxWidth = 0;
            GroupColumn.Width = new GridLength(0);
        }
        else
        {
            var settings = ServiceRegistry.Instance.UISettings.Settings;
            GroupColumn.MinWidth = 120;
            GroupColumn.MaxWidth = 400;
            GroupColumn.Width = new GridLength(Math.Max(120, settings.GroupColumnWidth));
            GroupSplitter.Visibility = Visibility.Visible;
            GroupPanel.Visibility = Visibility.Visible;
            MobileMenuButton.Visibility = Visibility.Collapsed;
            MobileMenuButton.IsChecked = false;
        }
    }

    private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };
        (VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement)?.RaiseEvent(eventArg);
    }

    private void GroupItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _dragStartPoint = e.GetPosition(null);

    private void GroupItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) return;

        if (sender is not ListBoxItem item || item.DataContext is not PresetGroup group) return;
        if (PresetManagerViewModel.IsVirtualGroup(group)) return;

        DragDrop.DoDragDrop(item, new DataObject("GroupFormat", group), DragDropEffects.Move);
        RemoveAdorner();
    }

    private void GroupItem_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("GroupFormat")) return;
        if (sender is not ListBoxItem item || item.DataContext is not PresetGroup group) return;
        if (PresetManagerViewModel.IsVirtualGroup(group)) return;
        CreateAdorner(item);
    }

    private void GroupItem_DragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void GroupItem_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();
        if (!e.Data.GetDataPresent("GroupFormat")) return;
        if (sender is not ListBoxItem item || item.DataContext is not PresetGroup target) return;
        if (e.Data.GetData("GroupFormat") is PresetGroup source && DataContext is PresetManagerViewModel vm)
            vm.MoveGroup(source, target);
    }

    private void GroupItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.DataContext is not PresetGroup group) return;
        if (DataContext is PresetManagerViewModel vm && vm.RenameGroupCommand.CanExecute(group))
        {
            vm.RenameGroupCommand.Execute(group);
            e.Handled = true;
        }
    }

    private void PresetItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _dragStartPoint = e.GetPosition(null);

    private void PresetItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) return;

        if (sender is not ListBoxItem item || item.DataContext is not PresetItemViewModel preset) return;
        if (DataContext is PresetManagerViewModel vm && vm.IsCurrentGroupVirtual) return;

        DragDrop.DoDragDrop(item, new DataObject("PresetFormat", preset), DragDropEffects.Move);
        RemoveAdorner();
    }

    private void PresetItem_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("PresetFormat")) return;
        if (DataContext is PresetManagerViewModel vm && vm.IsCurrentGroupVirtual) return;
        if (sender is ListBoxItem item)
            CreateAdorner(item);
    }

    private void PresetItem_DragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void PresetItem_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();
        if (!e.Data.GetDataPresent("PresetFormat")) return;
        if (sender is not ListBoxItem item || item.DataContext is not PresetItemViewModel target) return;
        if (e.Data.GetData("PresetFormat") is PresetItemViewModel source && DataContext is PresetManagerViewModel vm)
            vm.MovePreset(source, target);
    }

    private void PresetItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.DataContext is not PresetItemViewModel preset) return;
        if (DataContext is PresetManagerViewModel vm && vm.RenamePresetCommand.CanExecute(preset))
        {
            vm.RenamePresetCommand.Execute(preset);
            e.Handled = true;
        }
    }

    private void CreateAdorner(UIElement element)
    {
        RemoveAdorner();
        _adornerLayer = AdornerLayer.GetAdornerLayer(element);
        if (_adornerLayer is null) return;
        _insertionAdorner = new DropInsertionAdorner(element);
        _adornerLayer.Add(_insertionAdorner);
    }

    private void RemoveAdorner()
    {
        if (_adornerLayer is null || _insertionAdorner is null) return;
        _adornerLayer.Remove(_insertionAdorner);
        _insertionAdorner = null;
    }

    private void PresetButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button button || button.Content is not Grid mainGrid) return;
        if (mainGrid.Children[1] is not TextBlock textBlock) return;

        textBlock.Visibility = Visibility.Visible;
        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var targetWidth = button.ActualHeight + textBlock.DesiredSize.Width + 10;

        button.BeginAnimation(WidthProperty,
            new DoubleAnimation(button.ActualWidth, targetWidth, TimeSpan.FromMilliseconds(150)) { DecelerationRatio = 0.9 });
        textBlock.BeginAnimation(OpacityProperty,
            new DoubleAnimation(textBlock.Opacity, 1.0, TimeSpan.FromMilliseconds(150)));
    }

    private void PresetButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Button button || button.Content is not Grid mainGrid) return;
        if (mainGrid.Children[1] is not TextBlock textBlock) return;

        var widthAnim = new DoubleAnimation(button.ActualWidth, button.ActualHeight, TimeSpan.FromMilliseconds(150)) { DecelerationRatio = 0.9 };
        widthAnim.Completed += (_, _) =>
        {
            if (!button.IsMouseOver) textBlock.Visibility = Visibility.Collapsed;
        };

        button.BeginAnimation(WidthProperty, widthAnim);
        textBlock.BeginAnimation(OpacityProperty,
            new DoubleAnimation(textBlock.Opacity, 0.0, TimeSpan.FromMilliseconds(150)));
    }
}