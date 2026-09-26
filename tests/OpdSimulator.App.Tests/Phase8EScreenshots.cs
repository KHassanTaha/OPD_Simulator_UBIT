using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8E gate evidence — the two cosmetic/functional fixes captured
/// visually, run headlessly per the D-089 method.
///
/// <list type="bullet">
/// <item>(d) <c>phase-8e-dropdown-open.png</c> — a dropdown that already has a
/// value, opened, showing the FULL option list rather than the single committed
/// item. This is the visible half of Bug 3; the click-to-commit half is
/// asserted in <see cref="Phase8EFixTests"/>.</item>
/// <item>(f) <c>phase-8e-clear-dialog.png</c> — the Clear-All confirmation with
/// its new 24 px outer padding and the 16 / 24 px gaps (Bug 4).</item>
/// </list>
///
/// The exact pixel values are asserted in <see cref="Phase8EFixTests"/>; these
/// captures exist so a human can see the two surfaces in their fixed state.
/// </summary>
public class Phase8EScreenshots
{
    private static readonly string[] Options =
    {
        "Exponential", "Erlang", "Gamma", "Lognormal", "Normal", "Uniform", "Weibull",
    };

    [AvaloniaFact]
    public void Render_DropdownOpen_SavePhase8eDropdownOpenPng()
    {
        var dropdown = new SearchableDropdown
        {
            Label = "Arrival distribution",
            Tooltip = "Distribution family fitted to the uploaded inter-arrival times.",
            HelpAnchor = "arrival-distribution",
            Items = Options,
            SelectedItem = "Exponential",
            Placeholder = "e.g., Exponential",
        };

        var window = new Window
        {
            Width = 520,
            Height = 420,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Bug 3 — a dropdown that already has a value now opens on the FULL list",
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 16),
                    },
                    dropdown,
                },
            },
        };
        window.Show();

        try
        {
            var filterBox = dropdown.FindDescendantOfType<TextBox>()!;
            var popup = dropdown.FindDescendantOfType<Popup>()!;
            var list = (popup.Child as Border)!.FindDescendantOfType<ListBox>()!;

            // The state the user complained about: open a dropdown that already
            // holds a committed value. It used to show exactly one option.
            filterBox.RaiseEvent(new GotFocusEventArgs());

            Assert.True(popup.IsOpen, "the popup must be open for the capture");
            Assert.Equal(Options.Length, list.ItemCount);
            Assert.Equal("Exponential", list.SelectedItem);
            Assert.Equal(string.Empty, filterBox.Text);

            var frame = HeadlessScreenshot.Capture(window);
            Save(frame, "phase-8e-dropdown-open.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_ClearAllDialog_SavePhase8eClearDialogPng()
    {
        var dialog = new ThemedDialog
        {
            Title = "Clear all fields?",
            Message = "Every configuration field returns to its default and any loaded data file is "
                    + "released. This cannot be undone.",
        };
        dialog.PrimaryButton.Content = "Clear all";
        dialog.SecondaryButton.Content = "Cancel";
        dialog.Width = 480;
        dialog.Show();

        try
        {
            var frame = HeadlessScreenshot.Capture(dialog);
            Save(frame, "phase-8e-clear-dialog.png");
        }
        finally
        {
            dialog.Close();
        }
    }

    private static void Save(WriteableBitmap frame, string fileName)
    {
        var dir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        frame.Save(path);
        Assert.True(File.Exists(path) && new FileInfo(path).Length >= 512,
            $"{fileName} missing or suspiciously small");
    }

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpdSimulator.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
