using Avalonia;
using Avalonia.Controls;

namespace OpdSimulator.App.Controls;

/// <summary>
/// Hosts the primary action bar that stays pinned at the bottom of a
/// scrollable panel (FR-UI-11). The dimmed/disabled state is delegated to
/// the hosted control via its own resources — this bar supplies the chrome.
/// </summary>
public class PinnedFooterBar : ContentControl
{
    /// <summary>Creates the footer bar.</summary>
    public PinnedFooterBar()
    {
        // The chrome is provided by the type-keyed ControlTheme in
        // ControlStyles.axaml; the pinned Content slot is inherited content.
    }
}