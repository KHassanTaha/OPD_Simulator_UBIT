using System;

namespace OpdSimulator.App.Services;

/// <summary>
/// Pure expiry logic for toasts — separated from the UI so it is unit
/// testable and viva-defensible (a late-ticking host timer cannot influence
/// the decision about whether a toast is still visible).
/// </summary>
public static class ToastLifecycle
{
    /// <summary>Returns true when the toast has been visible for at least its duration.</summary>
    /// <param name="item">The toast to evaluate.</param>
    /// <param name="now">Current wall-clock time (UTC), injectable for tests.</param>
    public static bool IsExpired(ViewModels.ToastItem item, DateTimeOffset now)
        => now - item.CreatedUtc >= item.Duration;
}