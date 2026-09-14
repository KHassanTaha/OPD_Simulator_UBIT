using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Views;

/// <summary>
/// Main window shell: hosts the toast stack (FR-UI-10), the guide overlay
/// with its focus/escape contract (§17.1) and the F1 shortcut. All simulation
/// logic lives in <see cref="MainViewModel"/>; this class only glues hosted
/// controls to the view models.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<ToastItem, ThemedToast> _toastControls = new();
    private DispatcherTimer? _toastTimer;
    private IInputElement? _focusBeforeGuide;

    /// <summary>Creates the main window.</summary>
    public MainWindow()
    {
        InitializeComponent();

        KeyDown += OnWindowKeyDown;
        Opened += OnOpened;

        var vm = DataContext as MainViewModel;
        if (vm is not null)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            AttachToasts(vm.Toasts);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var vm = (MainViewModel)sender!;
        if (e.PropertyName == nameof(MainViewModel.IsGuideOpen))
        {
            if (vm.IsGuideOpen)
            {
                _focusBeforeGuide = FocusManager?.GetFocusedElement();
                Dispatcher.UIThread.Post(() => GuidePanelHost.FocusSearch());
            }
            else if (_focusBeforeGuide is Control opener)
            {
                opener.Focus();
            }
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _toastTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(600), DispatcherPriority.Background,
            (_, _) => (DataContext as MainViewModel)?.Toasts.PurgeExpired(DateTimeOffset.Now));
        _toastTimer.Start();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm is null)
        {
            return;
        }

        if (e.Key == Key.F1 && !vm.IsGuideOpen)
        {
            vm.OpenGuideCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && vm.IsGuideOpen)
        {
            vm.CloseGuideCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void AttachToasts(ToastService toasts)
    {
        toasts.Toasts.CollectionChanged += OnToastsChanged;
    }

    private void OnToastsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (ToastItem item in e.NewItems)
            {
                var card = new ThemedToast
                {
                    Message = item.Message,
                    ToastKind = item.Kind,
                    Margin = new Thickness(0, 0, 0, 8),
                };
                card.Dismissed += (_, _) => (DataContext as MainViewModel)?.Toasts.Dismiss(item);
                _toastControls[item] = card;
                ToastHost.Items.Add(card);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is not null)
        {
            foreach (ToastItem item in e.OldItems)
            {
                if (_toastControls.TryGetValue(item, out var card))
                {
                    ToastHost.Items.Remove(card);
                    _toastControls.Remove(item);
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        _toastTimer?.Stop();
        if (DataContext is MainViewModel vm)
        {
            vm.Toasts.Toasts.CollectionChanged -= OnToastsChanged;
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        base.OnClosed(e);
    }
}