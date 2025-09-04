using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CombinedEffect.Models;
using CombinedEffect.ViewModels;
using YukkuriMovieMaker.Commons;
using System.Windows.Threading;
using System;

namespace CombinedEffect.Views
{
    public partial class PresetManagerControl : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private readonly DispatcherTimer _clickTimer;
        private object? _lastClickedItem = null;


        public PresetManagerControl()
        {
            InitializeComponent();
            DataContextChanged += PresetManagerControl_DataContextChanged;

            _clickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _clickTimer.Tick += OnClickTimerTick;
        }

        private void PresetManagerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PresetManagerViewModel oldVm)
            {
                oldVm.BeginEdit -= OnViewModelBeginEdit;
                oldVm.EndEdit -= OnViewModelEndEdit;
            }
            if (e.NewValue is PresetManagerViewModel newVm)
            {
                newVm.BeginEdit += OnViewModelBeginEdit;
                newVm.EndEdit += OnViewModelEndEdit;
            }
        }

        private void OnClickTimerTick(object? sender, EventArgs e)
        {
            _clickTimer.Stop();
            var vm = DataContext as PresetManagerViewModel;
            if (vm != null && _lastClickedItem is EffectPreset)
            {
                vm.ApplyPresetCommand.Execute(null);
            }
            _lastClickedItem = null;
        }

        private void PresetItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is EffectPreset preset)
            {
                var vm = DataContext as PresetManagerViewModel;
                if (vm == null) return;

                if (e.ClickCount == 2)
                {
                    _clickTimer.Stop();
                    _lastClickedItem = preset;
                    _clickTimer.Start();
                    e.Handled = true;
                }
                else if (e.ClickCount >= 3)
                {
                    _clickTimer.Stop();
                    _lastClickedItem = null;
                    vm.StartEditingCommand.Execute(preset);
                    e.Handled = true;
                }
            }
        }

        private void GroupItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is PresetGroup group)
            {
                var vm = DataContext as PresetManagerViewModel;
                if (vm == null) return;

                if (e.ClickCount >= 3)
                {
                    vm.StartEditingGroupCommand.Execute(group);
                    e.Handled = true;
                }
            }
        }


        private void OnViewModelBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, e);
        private void OnViewModelEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, e);
    }
}