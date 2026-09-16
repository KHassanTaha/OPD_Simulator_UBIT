using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using OpdSimulator.App;

[assembly: AvaloniaTestApplication(typeof(OpdSimulator.App.Tests.TestAppBuilder))]

namespace OpdSimulator.App.Tests;

/// <summary>
/// Boots the real <see cref="App"/> on Avalonia's headless platform so UI
/// tests exercise the actual startup path (XAML load, merged Theme.axaml +
/// Motion.axaml, lifetime wiring) without needing a display server.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}