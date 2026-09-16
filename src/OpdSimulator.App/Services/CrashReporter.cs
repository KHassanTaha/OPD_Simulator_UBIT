using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace OpdSimulator.App.Services;

/// <summary>
/// Writes crash details to <c>logs/crash-YYYYMMDD.log</c> and surfaces a
/// user-friendly dialog pointing at that file (AGENTS §12.3, §12.5).
/// Each report is appended, never truncated, so history survives.
/// </summary>
internal static class CrashReporter
{
    /// <summary>
    /// Records an unexpected exception and informs the user.
    /// </summary>
    /// <param name="exception">The exception to report.</param>
    /// <param name="source">Where the exception escaped from (AppDomain, TaskScheduler, Dispatcher).</param>
    /// <param name="simulationState">
    /// Snapshot of the simulation at crash time (last event, clock, patient ID).
    /// "N/A" before any run; populated by the run coordinator from Phase 5.
    /// </param>
    public static void Report(Exception exception, string source, string simulationState = "N/A (no active simulation)")
    {
        var path = WriteCrashLog(exception, source, simulationState);

        Dispatcher.UIThread.Post(() =>
        {
            var mainWindow = ApplicationLifetimeMainWindow();
            var dialog = BuildDialog(exception, path);

            try
            {
                if (mainWindow is not null)
                {
                    dialog.ShowDialog(mainWindow);
                }
                else
                {
                    dialog.Show();
                }
            }
            catch
            {
                // The dialog failed to render (e.g. app already shutting down).
                // The crash log has already been written; nothing more to do.
            }
        });
    }

    /// <summary>
    /// True for the known harmless Wayland/Ubuntu quirk where a background task
    /// asks the (absent) <c>com.canonical.AppMenu.Registrar</c> DBus service and
    /// raises <c>ServiceUnknown</c>. This is not a real crash (D-107): it must be
    /// marked observed and logged at Information, never surfaced as a dialog or
    /// written to the crash log.
    /// </summary>
    /// <param name="exception">The exception under inspection (inner chain walked).</param>
    internal static bool IsIgnorableWaylandQuirk(Exception? exception)
    {
        for (Exception? ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex.Message.Contains("com.canonical.AppMenu.Registrar", StringComparison.Ordinal))
            {
                // The CLR type of the unwrapped DBus error varies by Tmds.DBus
                // version; the owner-observed class name is "ServiceUnknown",
                // and the full error name also appears in the message. Either
                // match means the missing global-menu service.
                if (ex.GetType().Name == "ServiceUnknown"
                    || ex.Message.Contains("org.freedesktop.DBus.Error.ServiceUnknown", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string WriteCrashLog(Exception exception, string source, string simulationState)
    {
        var logDir = Path.Combine(Environment.CurrentDirectory, "logs");
        Directory.CreateDirectory(logDir);

        var path = Path.Combine(logDir, $"crash-{DateTime.Now:yyyyMMdd}.log");
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

        var lines =
            $"--- Crash report ({source}) at {DateTime.Now:yyyy-MM-dd HH:mm:ss} (local) ---" + Environment.NewLine +
            $"App version : {version}" + Environment.NewLine +
            $"Command line: {string.Join(' ', Environment.GetCommandLineArgs())}" + Environment.NewLine +
            $"OS          : {Environment.OSVersion} / .NET {Environment.Version}" + Environment.NewLine +
            $"Simulation  : {simulationState}" + Environment.NewLine +
            exception + Environment.NewLine +
            new string('=', 72) + Environment.NewLine;

        File.AppendAllText(path, lines);
        return path;
    }

    /// <summary>Returns the main window if a desktop lifetime is available; otherwise null.</summary>
    private static Window? ApplicationLifetimeMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }

    private static Window BuildDialog(Exception exception, string crashLogPath)
    {
        var message = new TextBlock
        {
            Text =
                "An unexpected error occurred." + Environment.NewLine +
                Environment.NewLine +
                exception.Message + Environment.NewLine +
                Environment.NewLine +
                $"Details were written to: {crashLogPath}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 560,
        };

        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
        };

        var panel = new StackPanel
        {
            Children = { message, closeButton },
            Margin = new Thickness(24),
            Spacing = 16,
        };

        var dialog = new Window
        {
            Title = "Unexpected error",
            Content = panel,
            Width = 640,
            Height = 260,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        closeButton.Click += (_, _) => dialog.Close();
        return dialog;
    }
}