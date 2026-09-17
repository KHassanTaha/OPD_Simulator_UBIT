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
/// selector (FR-UI-14) must offer exactly the seven Results widgets — Metrics
/// table, Chi-square table, Event trace, Data preview, Per-server utilisation,
/// Queue length over time, Waiting-time distribution — no more, no fewer.
/// The XAML picker IS the contract, so this is a rendered-window test: the
/// seven checkboxes are read from the real ResultPanel (D-123 window seam),
/// and the view-model key set is cross-checked so the persisted preferences
/// and the "exactly seven" rule stay consistent.
/// </summary>
public class Phase6c6WidgetSelectorTests
{
    [AvaloniaFact]
    public void ResultsPanel_WidgetSelector_ListsExactlySevenResultsWidgets()
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

            string[] expected = { "Metrics", "Chi-square", "Event trace", "Data preview",
                "Utilisation", "Queue length", "Wait histogram" };
            string[] actual = picker.GetVisualDescendants().OfType<CheckBox>()
                .Select(c => c.Content?.ToString() ?? string.Empty)
                .ToArray();
            Assert.Equal(expected, actual);

            // VM contract matches the UI exactly: these seven keys, no more.
            // A fresh panel (no persisted ui.json) proves the contract hermetically
            // — MainWindow's instance reflects this machine's real preferences file
            // and is not the thing under test here. Data preview starts off until a
            // file loads (FR-UI-14), so the initial set is six keys; the selector
            // lists all seven, and enabling it reaches the full set.
            var results = new ResultsPanelViewModel();
            Assert.Equal(7, expected.Length);
            Assert.Equal(new[] { "metrics", "chiSquare", "trace",
                "utilisation", "queueLength", "waitHistogram" }, results.VisibleWidgets);
            results.ToggleWidget("dataPreview");
            Assert.True(results.ShowDataPreview);
            Assert.Equal(new[] { "metrics", "chiSquare", "trace", "dataPreview",
                "utilisation", "queueLength", "waitHistogram" }, results.VisibleWidgets);

            // An unknown key must not introduce an eighth widget or throw.
            results.ToggleWidget("bogus-widget");
            Assert.Equal(7, results.VisibleWidgets.Count);
        }
        finally
        {
            window.Close();
        }
    }
}