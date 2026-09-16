using System;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// A single toast notification displayed by <see cref="Controls.ThemedToast"/>.
/// Severity drives the theme colours and icon; Messages are the user-facing text.
/// </summary>
public sealed record ToastItem(
    string Severity,
    string Message,
    DateTimeOffset Timestamp = default)
{
    /// <summary>Creates an info toast timestamped now.</summary>
    public static ToastItem Info(string message)
        => new("info", message, DateTimeOffset.Now);

    /// <summary>Creates a success toast timestamped now.</summary>
    public static ToastItem Success(string message)
        => new("success", message, DateTimeOffset.Now);

    /// <summary>Creates a warning toast timestamped now.</summary>
    public static ToastItem Warning(string message)
        => new("warning", message, DateTimeOffset.Now);

    /// <summary>Creates an error toast timestamped now.</summary>
    public static ToastItem Error(string message)
        => new("error", message, DateTimeOffset.Now);
}