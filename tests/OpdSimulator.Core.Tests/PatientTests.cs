using OpdSimulator.Core.Patients;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Tests for <see cref="Patient"/> stage movement, the mechanism behind
/// Reception → Screening → Doctor routing in the engine.
/// </summary>
public class PatientTests
{
    [Fact]
    public void AdvanceToStage_ReanchorsArrivalAndResetsServiceMilestones()
    {
        var patient = new Patient(id: 1, arrivalTime: 2.0, stageIndex: 0);

        patient.MarkServiceStarted(2.5);
        patient.MarkServiceCompleted(4.0);

        patient.AdvanceToStage(stageIndex: 1, arrivalTime: 4.0);

        Assert.Equal(1, patient.StageIndex);
        Assert.Equal(4.0, patient.ArrivalTime);
        Assert.False(patient.HasStartedService, "service milestones belong to the stage just left");
        Assert.False(patient.HasCompletedService);
        Assert.Equal(2.0, patient.SystemArrivalTime);
    }

    [Fact]
    public void AdvanceToStage_BeforeSystemArrival_Throws()
    {
        var patient = new Patient(id: 1, arrivalTime: 10.0, stageIndex: 0);

        Assert.Throws<ArgumentException>(() => patient.AdvanceToStage(stageIndex: 1, arrivalTime: 5.0));
    }

    [Fact]
    public void WaitTimeAndSystemTime_ReflectCurrentStageVisit()
    {
        var patient = new Patient(1, arrivalTime: 1.0, stageIndex: 0);
        patient.MarkServiceStarted(3.0);
        patient.MarkServiceCompleted(7.0);

        Assert.Equal(2.0, patient.WaitTimeMinutes);
        Assert.Equal(6.0, patient.SystemTimeMinutes);
        Assert.Equal(1.0, patient.SystemArrivalTime);
    }
}