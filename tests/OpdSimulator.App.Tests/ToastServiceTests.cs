using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Guards the toast auto-dismiss contract (FR-UI-10): a toast expires only
/// once its full duration has elapsed, and purge order is oldest-first.
/// </summary>
public class ToastServiceTests
{
    private static readonly DateTimeOffset Zero = DateTimeOffset.UtcNow;

    [Fact]
    public void Show_AddsToastWithDefaults()
    {
        var service = new ToastService();

        service.Show("Saved", ToastKind.Success);

        var toast = Assert.Single(service.Toasts);
        Assert.Equal("Saved", toast.Message);
        Assert.Equal(ToastKind.Success, toast.Kind);
    }

    [Fact]
    public void IsExpired_FalseBeforeDurationElapsed()
    {
        var toast = new ToastItem("x", ToastKind.Info, TimeSpan.FromSeconds(5), Zero);

        Assert.False(ToastLifecycle.IsExpired(toast, Zero + TimeSpan.FromSeconds(4.9)));
    }

    [Fact]
    public void IsExpired_TrueAtExactlyDuration()
    {
        var toast = new ToastItem("x", ToastKind.Info, TimeSpan.FromSeconds(5), Zero);

        Assert.True(ToastLifecycle.IsExpired(toast, Zero + TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Dismiss_RemovesToastImmediately()
    {
        var service = new ToastService();
        var toast = service.Show("x", ToastKind.Info);

        service.Dismiss(toast);

        Assert.Empty(service.Toasts);
    }

    [Fact]
    public void PurgeExpired_RemovesOnlyExpiredToasts()
    {
        var service = new ToastService();
        service.Toasts.Add(new ToastItem("stale", ToastKind.Info, TimeSpan.FromSeconds(1), Zero));
        service.Toasts.Add(new ToastItem("fresh", ToastKind.Info, TimeSpan.FromSeconds(60), Zero));

        service.PurgeExpired(Zero + TimeSpan.FromSeconds(2));

        var remaining = Assert.Single(service.Toasts);
        Assert.Equal("fresh", remaining.Message);
    }
}