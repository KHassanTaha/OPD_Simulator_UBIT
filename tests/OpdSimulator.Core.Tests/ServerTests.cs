using OpdSimulator.Core.Servers;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Tests for <see cref="Server"/>: busy/idle life-cycle, busy-time accumulation,
/// and the 0 ≤ utilisation ≤ 1 assertion (FR-VAL-2).
/// </summary>
public class ServerTests
{
    [Fact]
    public void NewServer_IsIdleWithZeroUtilisation()
    {
        var server = new Server(0);
        Assert.False(server.IsBusy);
        Assert.Equal(0, server.PatientsServed);
        Assert.Equal(0, server.Utilisation(100.0));
    }

    [Fact]
    public void StartAndEndService_AccumulatesBusyTime()
    {
        var server = new Server(0);
        server.StartService(10.0);
        Assert.True(server.IsBusy);

        server.EndService(25.0);
        Assert.False(server.IsBusy);
        Assert.Equal(1, server.PatientsServed);
        Assert.Equal(15.0, server.BusyTimeMinutes);
    }

    [Fact]
    public void Utilisation_IsBusyTimeOverOperatingTime()
    {
        var server = new Server(0);
        server.StartService(0.0);
        server.EndService(80.0);

        Assert.Equal(0.8, server.Utilisation(100.0));
    }

    [Fact]
    public void Utilisation_StaysWithinUnitInterval()
    {
        var server = new Server(0);
        server.StartService(0.0);
        server.EndService(5.0);

        double util = server.Utilisation(10.0);
        Assert.InRange(util, 0.0, 1.0);
    }

    [Fact]
    public void StartService_WhenAlreadyBusy_Throws()
    {
        var server = new Server(0);
        server.StartService(0.0);
        Assert.Throws<InvalidOperationException>(() => server.StartService(1.0));
    }

    [Fact]
    public void EndService_WhenIdle_Throws()
    {
        var server = new Server(0);
        Assert.Throws<InvalidOperationException>(() => server.EndService(1.0));
    }
}