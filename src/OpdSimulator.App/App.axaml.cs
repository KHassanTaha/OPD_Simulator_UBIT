using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using OpdSimulator.App.Services;
using OpdSimulator.App.Views;
using Serilog;

namespace OpdSimulator.App;

/// <summary>
/// Avalonia application root: owns the global exception handlers (AGENTS
/// §12.3) and creates the main window.
/// </summary>
public partial class App : Application
{
    static App()
    {
        // AGENTS §12.3 — every unhandled exception is logged to the crash file
        // (and shown as a dialog) instead of being silently swallowed.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            CrashReporter.Report(ex ?? new Exception("Unhandled exception with no exception object."), "AppDomain");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            // D-107: the missing Ubuntu global-menu service raises a benign
            // "com.canonical.AppMenu.Registrar" DBus error on Wayland builds.
            // It is ignored (never a dialog, never a crash-log row) and only
            // logged as Information.
            if (CrashReporter.IsIgnorableWaylandQuirk(e.Exception))
            {
                Log.Information("Ignoring known Wayland DBus quirk: {Message}", e.Exception.Message);
                e.SetObserved();
                return;
            }

            CrashReporter.Report(e.Exception, "TaskScheduler");
            e.SetObserved();
        };
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        PinSystemAccentToBrand();

        if (Dispatcher.UIThread != null)
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                CrashReporter.Report(e.Exception, "Dispatcher");
                e.Handled = true;
            };
        }
    }

    /// <summary>
    /// Pins Fluent's system accent to the brand green (Phase 8F, D-141).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Avalonia's Fluent theme asks the operating system for an accent colour
    /// and derives its highlight brushes from it. That made the UI
    /// machine-dependent: the CollapsibleSection header rendered blue on a
    /// machine whose OS accent was blue and orange on another. Setting
    /// <c>RequestedThemeVariant="Light"</c> does not disable the lookup, and
    /// neither does defining <c>SystemAccentColor</c> in Theme.axaml — in
    /// Avalonia 11.3.3 no such resource key exists or is consulted.
    /// </para>
    /// <para>
    /// The supported override point is <see cref="FluentTheme.Palettes"/>: a
    /// <c>ColorPaletteResources</c> instance per theme variant, whose
    /// <c>Accent</c> colour feeds <c>SystemControlHighlightAccentBrush</c>.
    /// Verified by measurement: with no entry the brush resolves to the
    /// machine accent (<c>#0078D7</c> on the test host) and with an Accent-only
    /// entry it resolves to the injected colour, while 24 of 25 other probed
    /// Fluent brush resources are unchanged — <c>ColorPaletteResources</c>
    /// exposes a single <c>Accent</c> knob, not the Light1/Dark1 ladder.
    /// </para>
    /// <para>
    /// The colour is read from Theme.axaml rather than written here, so the hex
    /// still lives in exactly one file (AGENTS §16.3). An existing palette entry
    /// is mutated in place instead of replaced, so any future Avalonia version
    /// that seeds non-accent palette values keeps them. Only the Light and Dark
    /// variants are accepted by <c>Palettes</c>; adding Default throws.
    /// </para>
    /// </remarks>
    private void PinSystemAccentToBrand()
    {
        if (!Resources.TryGetResource("ColorBrandGreen", null, out var brand) || brand is not Color brandColor)
        {
            Log.Warning(
                "Accent override skipped: theme resource 'ColorBrandGreen' did not resolve, so the OS accent may leak into the UI.");
            return;
        }

        var fluent = Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluent is null)
        {
            Log.Warning("Accent override skipped: no FluentTheme found in Application.Styles.");
            return;
        }

        // Palettes accepts Light and Dark only — adding ThemeVariant.Default
        // throws "FluentTheme.Palettes only supports Light and Dark variants".
        // Default is not needed: Fluent resolves an unrecognised variant to the
        // Light palette, and the app pins RequestedThemeVariant="Light".
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            if (fluent.Palettes.TryGetValue(variant, out var existing) && existing is not null)
            {
                existing.Accent = brandColor;
            }
            else
            {
                fluent.Palettes[variant] = new ColorPaletteResources { Accent = brandColor };
            }
        }

        Log.Information("Pinned the Fluent system accent to {Brand} for the Light and Dark variants.", brandColor);
    }

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) => Log.Information("Application exiting.");
            Log.Information("Application started. Main window created.");
        }

        base.OnFrameworkInitializationCompleted();
    }
}