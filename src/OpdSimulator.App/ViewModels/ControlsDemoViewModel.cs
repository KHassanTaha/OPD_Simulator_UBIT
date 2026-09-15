using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Controls;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// Drives the Phase-2 controls demo page. Each control gets a live instance the
/// owner can click/tab through; this model supplies the data and the validation
/// rule so the reusable controls are demonstrated (not just placed).
/// </summary>
public partial class ControlsDemoViewModel : ObservableObject
{
    private readonly DispatcherTimer _toastExpiry = new()
    {
        Interval = TimeSpan.FromSeconds(6),
    };

    public ControlsDemoViewModel()
    {
        _toastExpiry.Tick += OnToastExpiryTick;
    }

    /// <summary>Distributions offered by the searchable dropdown demo.</summary>
    public IReadOnlyList<string> Distributions { get; } =
        new[]
        {
            "Exponential", "Normal", "Uniform", "Erlang (gamma)", "Empirical (from data)",
        };

    /// <summary>Selected distribution (two-way via the dropdown).</summary>
    [ObservableProperty]
    private string? _selectedDistribution = "Exponential";

    /// <summary>The arrival-rate field value.</summary>
    [ObservableProperty]
    private string _arrivalRate = "0.5";

    /// <summary>True while the arrival-rate value is invalid (FR-UI-17).</summary>
    [ObservableProperty]
    private bool _arrivalRateHasError;

    /// <summary>Cause + remedy message shown when the field is invalid.</summary>
    [ObservableProperty]
    private string? _arrivalRateError;

    /// <summary>Toasts currently on screen (ThemedToast demo).</summary>
    public ObservableCollection<ToastItem> Toasts { get; } = new();

    /// <summary>FR-UI-9 error-banner summary; null hides the banner.</summary>
    [ObservableProperty]
    private string? _bannerMessage;

    /// <summary>Reflected in the pinned-footer status line.</summary>
    [ObservableProperty]
    private string _footerStatus = "Idle — press the primary action.";

    /// <summary>Disables/enables the demo primary button.</summary>
    [ObservableProperty]
    private bool _primaryEnabled = true;

    /// <summary>Whether the collapsible section is open.</summary>
    [ObservableProperty]
    private bool _sectionOpen = true;

    /// <summary>Pinned-footer primary action (sets the status line).</summary>
    public ICommand PrimaryActionCommand => new RelayCommand(() =>
    {
        FooterStatus = "Primary action executed.";
    });

    /// <summary>Toggles the primary button disabled state (with reason tooltip).</summary>
    public ICommand TogglePrimaryCommand => new RelayCommand(() =>
    {
        PrimaryEnabled = !PrimaryEnabled;
        FooterStatus = PrimaryEnabled ? "Primary button re-enabled." : "Primary button disabled.";
    });

    /// <summary>Shows the demo error banner with a cause + remedy message.</summary>
    public ICommand ShowBannerCommand => new RelayCommand(() =>
    {
        BannerMessage = "No simulation has run yet. Load a data file or enter parameters, then press Start Calculation.";
    });

    /// <summary>Clears the demo error banner.</summary>
    public ICommand ClearBannerCommand => new RelayCommand(() => BannerMessage = null);

    /// <summary>Validates the arrival-rate field on blur (FR-UI-17).</summary>
    public void ValidateArrivalRate()
    {
        if (!double.TryParse(ArrivalRate, out var lambda))
        {
            ArrivalRateHasError = true;
            ArrivalRateError = $"Arrival rate must be a positive number. You entered \"{ArrivalRate}\".";
            return;
        }

        if (lambda <= 0)
        {
            ArrivalRateHasError = true;
            ArrivalRateError = $"Arrival rate must be greater than 0. You entered {lambda}.";
            return;
        }

        ArrivalRateHasError = false;
        ArrivalRateError = null;
    }

    /// <summary>Appends a toast of the given severity; expires after 6 s.</summary>
    public void ShowToast(string severity, string message)
    {
        Toasts.Add(new ToastItem(severity, message));
        RestartToastExpiry();
    }

    /// <summary>Dismisses the given toast.</summary>
    public ICommand DismissToastCommand => new RelayCommand<ToastItem>(DismissToast);

    private void DismissToast(ToastItem? toast)
    {
        if (toast is not null)
        {
            Toasts.Remove(toast);
        }
    }

    /// <summary>Demo data for the preview table: 1,000 rows, one invalid.</summary>
    public IReadOnlyList<string> PreviewHeaders { get; } =
        new[] { "Ticket", "Arrival time", "Queue", "Wait (min)", "Verified" };

    public IReadOnlyList<IReadOnlyList<string?>> PreviewRows { get; } = BuildPreviewRows();

    /// <summary>Row index 42 is invalid with a specific validator reason.</summary>
    public IReadOnlyDictionary<int, string?> PreviewInvalidRows { get; } =
        new Dictionary<int, string?>() { [42] = "Wait (min) is negative; expected ≥ 0." };

    private static IReadOnlyList<IReadOnlyList<string?>> BuildPreviewRows()
    {
        var rows = new List<IReadOnlyList<string?>>(1000);
        var rng = new Random(1234);
        for (int i = 0; i < 1000; i++)
        {
            var wait = Math.Round(rng.NextDouble() * 48, 1);
            if (i == 42)
            {
                wait = -1.5;
            }

            rows.Add(new[] { $"T{i + 1}", $"09:{i % 60:00}:{i % 60:00}", "Reception", wait.ToString("0.0"), "yes" });
        }

        return rows;
    }

    private void OnToastExpiryTick(object? sender, EventArgs e)
    {
        if (Toasts.Count == 0)
        {
            _toastExpiry.Stop();
            return;
        }

        Toasts.RemoveAt(0);

        if (Toasts.Count > 0)
        {
            _toastExpiry.Stop();
            _toastExpiry.Start();
        }
    }

    private void RestartToastExpiry()
    {
        _toastExpiry.Stop();
        _toastExpiry.Start();
    }
}