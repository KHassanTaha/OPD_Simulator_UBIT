using OpdSimulator.App.ViewModels;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// FR-UI-5: the welcome card must degrade to text when no Avalonia app
/// session / asset tree is available (exactly what a headless test run or a
/// broken install looks like) instead of throwing. Course identity and
/// member list must always come straight from the CourseInfo constants.
/// </summary>
public sealed class WelcomeCardViewModelTests
{
    [Fact]
    public void Describe_CourseConstants_AreExposedUnchanged()
    {
        var vm = new WelcomeCardViewModel();

        Assert.Equal(OpdSimulator.App.CourseInfo.CourseName, vm.CourseName);
        Assert.Equal(OpdSimulator.App.CourseInfo.CourseCode, vm.CourseCode);
        Assert.Equal(OpdSimulator.App.CourseInfo.Professor, vm.Professor);
        Assert.Same(OpdSimulator.App.CourseInfo.Members, vm.Members);
    }

    [Fact]
    public void Logos_WithoutAppSession_ReturnNullInsteadOfThrowing()
    {
        var vm = new WelcomeCardViewModel();

        Assert.Null(vm.UokLogoImage);
        Assert.Null(vm.UbitLogoImage);
    }
}