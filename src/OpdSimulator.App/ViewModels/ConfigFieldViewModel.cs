using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// Wraps a single validated input field: its string value, error state and
/// error message. Validation is blur-based (AGENTS §16.9): the value setter
/// clears any existing error so a field stops looking wrong the instant the
/// user starts editing it, and re-validation happens on <c>LostFocus</c>.
/// </summary>
public partial class ConfigFieldViewModel : ObservableObject
{
    private string? _value;

    /// <summary>
    /// Raised whenever <see cref="Value"/> changes. Consumers (e.g. the ρ
    /// summary) use this to recompute derived output live.
    /// </summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// Gets or sets the raw string the user typed into the field. Setting it
    /// clears any pending error (FR-UI-17: valid-after-error clears at once)
    /// and raises <see cref="ValueChanged"/>.
    /// </summary>
    public string? Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                HasError = false;
                ErrorMessage = null;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Gets or sets whether the field currently shows an inline error (FR-UI-17).</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// Gets or sets the cause + remedy message shown when the field is invalid
    /// ("Arrival rate must be greater than 0. You entered 0." — never generic).
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Marks the field invalid with the given cause + remedy message.
    /// </summary>
    /// <param name="message">The inline error text to display under the field.</param>
    public void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    /// <summary>Clears any inline error (used on blur when the value is valid).</summary>
    public void ClearError()
    {
        HasError = false;
        ErrorMessage = null;
    }
}