using System;
using System.IO;
using Avalonia;
using Serilog;
using Serilog.Events;

namespace OpdSimulator.App;

/// <summary>
/// Application entry point for the Avalonia desktop app.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Initializes the Avalonia application and Serilog logging.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the app (currently unused).</param>
    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureLogging();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Builds the Avalonia <see cref="AppBuilder"/> for this application.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>
    /// Configures the process-wide Serilog pipeline: console sink, a rolling
    /// file sink (7-day retention, AGENTS §12.1) and a separate error-only
    /// rolling file sink. Both file sinks share the underlying handler so the
    /// console and file sinks never contend for the app log file.
    /// </summary>
    private static void ConfigureLogging()
    {
        var logDirectory = Path.Combine(Environment.CurrentDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true)
            .WriteTo.Logger(fileOnly => fileOnly
                .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Warning)
                .WriteTo.File(
                    Path.Combine(logDirectory, "errors-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7))
            .CreateLogger();
    }
}