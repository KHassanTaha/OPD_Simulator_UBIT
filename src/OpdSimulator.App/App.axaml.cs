using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            CrashReporter.Report(e.Exception, "TaskScheduler");
            e.SetObserved();
        };
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        if (Dispatcher.UIThread != null)
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                CrashReporter.Report(e.Exception, "Dispatcher");
                e.Handled = true;
            };
        }
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