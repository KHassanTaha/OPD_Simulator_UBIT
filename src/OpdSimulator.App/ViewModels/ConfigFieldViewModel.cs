namespace OpdSimulator.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// One user-editable config field: its raw text value plus the FR-UI-17
/// error state (border + icon + actionable message). Wrapping each field in
/// its own observable lets the <c>ValidatedField</c> wrapper bind error state
/// directly, and editing any value clears that field's error immediately so
/// "fixing a field clears its red border" (AGENTS §16.9) is impossible to
/// forget.
/// </summary>
public sealed partial class ConfigFieldViewModel : ViewModelBase
{
    /// <summary>Creates a field.</summary>
    /// <param name="key">Stable key used for focus-first-invalid navigation.</param>
    /// <param name="defaultValue">Initial text; fields start empty unless a default is meaningful.</param>
    public ConfigFieldViewModel(string key, string? defaultValue = null)
    {
        Key = key;
        _value = defaultValue ?? string.Empty;
    }

    /// <summary>Gets the stable key (focus navigation and error reporting).</summary>
    public string Key { get; }

    /// <summary>Gets or sets the raw field text.</summary>
    [ObservableProperty]
    private string _value;

    /// <summary>Gets or sets whether the field is currently invalid.</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>Gets or sets the actionable inline error message.</summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    partial void OnValueChanged(string value)
    {
        // Editing is the user fixing the problem — the red treatment must go
        // away at once, not at the next submit (AGENTS §16.9).
        ClearError();
    }

    /// <summary>Clears the error state.</summary>
    public void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    /// <summary>Sets the error state with an actionable message.</summary>
    /// <param name="message">"What is wrong AND what is expected", e.g.
    /// "Arrival rate must be greater than 0. You entered 0."</param>
    public void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    /// <summary>Restores the field to a default value with no error.</summary>
    /// <param name="defaultValue">Text to restore; empty by default.</param>
    public void Reset(string? defaultValue = null)
    {
        Value = defaultValue ?? string.Empty;
        ClearError();
    }
}