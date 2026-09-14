using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpdSimulator.App.ViewModels;

/// <summary>Toast severity; selects the themed variant shown on the card.</summary>
public enum ToastKind
{
    /// <summary>Confirmation-style notification.</summary>
    Success,

    /// <summary>Failure/warning-style notification.</summary>
    Error,

    /// <summary>Neutral informational notification.</summary>
    Info,
}

/// <summary>
/// A single toast as shown in the notification stack: message, severity and
/// the window in which it expires (pure data, no UI dependency).
/// </summary>
public sealed partial class ToastItem : ViewModelBase
{
    /// <summary>Creates a toast item.</summary>
    /// <param name="message">Text shown on the card.</param>
    /// <param name="kind">Severity variant.</param>
    /// <param name="duration">How long the card stays visible before expiring.</param>
    public ToastItem(string message, ToastKind kind, TimeSpan duration)
    {
        Message = message;
        Kind = kind;
        Duration = duration;
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the message text.</summary>
    [ObservableProperty]
    private string _message;

    /// <summary>Gets the severity variant.</summary>
    [ObservableProperty]
    private ToastKind _kind;

    /// <summary>Gets the wall-clock time the toast was created (UTC).</summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>Gets how long the toast stays visible.</summary>
    public TimeSpan Duration { get; }
}