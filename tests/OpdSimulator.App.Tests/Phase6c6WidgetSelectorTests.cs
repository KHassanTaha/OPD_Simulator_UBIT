using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.6 — widget integration verification: the "Customise results"
/// selector (FR-UI-14) must offer exactly the six Results widgets — Metrics
/// table, Chi-square table, Event trace, Per-server utilisation, Queue length
/// over time, Waiting-time distribution — no more, no fewer. Phase 7D removed
/// the Data preview widget (it now lives on the Input tab). The XAML picker IS
/// the contract, so this is a rendered-window test: the six checkboxes are read
/// from the real ResultPanel (D-123 window seam), and the view-model key set is
/// cross-checked so the persisted preferences and the "exactly six" rule stay
/// consistent.
/// </summary>
public class Phase6c6WidgetSelectorTests
{
    [AvaloniaFact]
    public void ResultsPanel_WidgetSelector_ListsExactlySixResultsWidgets()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            // The selector lives in the named WidgetPicker border; it is shown
            // here so the checkboxes are realised and enumerable (the picker is
            // rendered inside the Simulation tab, which is the selected tab, so
            // its content exists — D-095).
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
            var picker = panel.GetVisualDescendants().OfType<Border>()
                .Single(b => string.Equals(b.Name, "WidgetPicker", StringComparison.Ordinal));
            picker.IsVisible = true;
            window.UpdateLayout();

            string[] expected = { "Metrics", "Chi-square", "Event trace",
                "Utilisation", "Queue length", "Wait histogram" };
            string[] actual = picker.GetVisualDescendants().OfType<CheckBox>()
                .Select(c => c.Content?.ToString() ?? string.Empty)
                .ToArray();
            Assert.Equal(expected, actual);

            // VM contract matches the UI exactly: these six keys, no more.
            // A fresh panel (no persisted ui.json) proves the contract hermetically
            // — MainWindow's instance reflects this machine's real preferences file
            // and is not the thing under test here. Phase 7D removed the
            // "dataPreview" widget from the results panel, so the initial set is
            // now all six keys.
            var results = new ResultsPanelViewModel();
            Assert.Equal(6, expected.Length);
            Assert.Equal(new[] { "metrics", "chiSquare", "trace",
                "utilisation", "queueLength", "waitHistogram" }, results.VisibleWidgets);

            // An unknown key must not introduce a seventh widget or throw.
            results.ToggleWidget("bogus-widget");
            Assert.Equal(6, results.VisibleWidgets.Count);

            // The removed key is inert: toggling it changes nothing.
            results.ToggleWidget("dataPreview");
            Assert.Equal(6, results.VisibleWidgets.Count);
        }
        finally
        {
            window.Close();
        }
    }
}
