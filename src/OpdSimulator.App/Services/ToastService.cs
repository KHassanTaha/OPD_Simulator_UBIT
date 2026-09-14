using System;
using System.Collections.ObjectModel;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Services;

/// <summary>
/// Holds the live toast stack. The hosting control subscribes to the
/// collection and owns the remove timer; this service is UI-agnostic so
/// view-model tests can drive it without an Avalonia session.
/// </summary>
public sealed class ToastService
{
    /// <summary>Default visibility window when a caller does not override it.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);

    /// <summary>Gets the live stack of toasts, oldest first.</summary>
    public ObservableCollection<ToastItem> Toasts { get; } = new();

    /// <summary>Adds a toast to the stack.</summary>
    /// <returns>The created item, so callers can dismiss it early.</returns>
    public ToastItem Show(string message, ToastKind kind, TimeSpan? duration = null)
    {
        var item = new ToastItem(message, kind, duration ?? DefaultDuration);
        Toasts.Add(item);
        return item;
    }

    /// <summary>Removes a toast immediately (user-close or public dismissal).</summary>
    public void Dismiss(ToastItem item)
    {
        Toasts.Remove(item);
    }

    /// <summary>Removes every toast whose window has elapsed (host timer tick).</summary>
    public void PurgeExpired(DateTimeOffset now)
    {
        // Iterate back-to-front so removal never shifts an unvisited index.
        for (var i = Toasts.Count - 1; i >= 0; i--)
        {
            if (ToastLifecycle.IsExpired(Toasts[i], now))
            {
                Toasts.RemoveAt(i);
            }
        }
    }
}