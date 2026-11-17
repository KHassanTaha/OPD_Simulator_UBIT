namespace OpdSimulator.App.Tests;

using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using OpdSimulator.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Analytics;
using OpdSimulator.Data.Parameters;
using Xunit;

public sealed class Phase9QTheoryTests
{
    private const double Lambda = 0.5;
    private const double Mu = 0.8;
    private const double Rho = Lambda / Mu;

    [Fact]
    public void Builder_Mm1_MatchesCoreErlangC()
    {
        Assert.True(AnalyticalModels.TryCompute(
            AnalyticalModelFamily.Mmc,
            new AnalyticalModelInputs(Lambda, Mu, 1, 1, 1),
            out var core,
            out _));
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Mm1, Lambda, Mu, 1, 1, out var theory, out _));

        Assert.Equal(core!.Wq, theory!.Result.Wq, 12);
        Assert.Equal(core.Lq, theory.Result.Lq, 12);
        Assert.Equal(core.W, theory.Result.W, 12);
        Assert.Contains("Erlang-C", theory.Result.FormulaName);
    }

    [Fact]
    public void Builder_Mm1_UsesTheExactSingleServerFormula()
    {
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Mm1, Lambda, Mu, 1, 1, out var solution, out _));

        double expected = Rho / (Mu * (1.0 - Rho));
        Assert.Equal(expected, solution!.Result.Wq, 10);
        Assert.Equal(1.0 - Rho, solution.Result.P0, 10);
    }

    [Fact]
    public void Builder_Mg1_UsesPollaczekKhinchine()
    {
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Mg1, Lambda, Mu, 1, 0, out var solution, out _));

        double expected = Rho / (2.0 * Mu * (1.0 - Rho));
        Assert.Equal(expected, solution!.Result.Wq, 10);
        Assert.Contains("Pollaczek–Khinchine", solution.Result.FormulaName);
        Assert.Contains("P(wait)", solution.Rows.Select(r => r.Symbol));
    }

    [Fact]
    public void Builder_Gg1_UsesKingmanAndOmitsPWait()
    {
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Gg1, Lambda, Mu, 0, 1, out var solution, out _));

        double expected = Rho / (Mu * (1.0 - Rho)) * 0.5;
        Assert.Equal(expected, solution!.Result.Wq, 10);
        Assert.DoesNotContain("P(wait)", solution.Rows.Select(r => r.Symbol));
        Assert.Contains("two-moment approximation", solution.Result.FormulaName);
        Assert.Contains("not exact in general", solution.ApproximationNotice);
        Assert.DoesNotContain("exact for c = 1", solution.Result.FormulaName);
    }

    [Fact]
    public void Builder_Gg1_WithPoissonArrivals_ReducesToMg1()
    {
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Mg1, Lambda, Mu, 1, 0.25, out var mg1, out _));
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Gg1, Lambda, Mu, 1, 0.25, out var gg1, out _));

        Assert.Equal(mg1!.Result.Wq, gg1!.Result.Wq, 12);
    }

    [Fact]
    public void Builder_SerialNumbers_AreContiguousFromOne()
    {
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Gg1, Lambda, Mu, 1, 1, out var solution, out _));

        Assert.Equal(
            Enumerable.Range(1, solution!.Rows.Count),
            solution.Rows.Select(r => r.SerialNumber));
    }

    [Fact]
    public void Variability_CvVarianceSecondMoment_ProduceTheSameAnswer()
    {
        const double cv = 0.5;
        double mean = 1.0 / Mu;
        double variance = cv * cv * mean * mean;
        double secondMoment = mean * mean + variance;

        var vm = new TheoryTabViewModel();
        vm.SetModel(TheoryModel.Mg1);
        vm.SetVariabilityMode(VariabilityMode.Cv);
        vm.ServiceVariabilityInput = "0.5";
        double byCv = vm.Solution!.Result.Wq;

        vm.SetVariabilityMode(VariabilityMode.Variance);
        vm.ServiceVariabilityInput = TheoryWorkedSolutionBuilder.VariabilityText(
            0.25, mean, VariabilityMode.Variance);
        double byVariance = vm.Solution!.Result.Wq;

        vm.SetVariabilityMode(VariabilityMode.SecondMoment);
        vm.ServiceVariabilityInput = TheoryWorkedSolutionBuilder.VariabilityText(
            0.25, mean, VariabilityMode.SecondMoment);
        double bySecondMoment = vm.Solution!.Result.Wq;

        Assert.Equal(variance, 0.25 * mean * mean, 10);
        Assert.Equal(secondMoment, 1.25 * mean * mean, 10);
        Assert.Equal(byCv, byVariance, 10);
        Assert.Equal(byCv, bySecondMoment, 10);
    }

    [Fact]
    public void Variability_SecondMomentBelowMeanSquared_IsRefused()
    {
        var vm = new TheoryTabViewModel();
        vm.SetModel(TheoryModel.Gg1);
        vm.SetVariabilityMode(VariabilityMode.SecondMoment);

        vm.ServiceVariabilityInput = "0.5";
        vm.ValidateField("service");

        Assert.Null(vm.Solution);
        Assert.True(vm.ServiceVariabilityHasError);
        Assert.Contains("second moment", vm.ServiceVariabilityError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Variability_CvZero_IsDeterministicAndValid()
    {
        var vm = new TheoryTabViewModel();
        vm.SetModel(TheoryModel.Mg1);

        vm.ServiceVariabilityInput = "0";

        Assert.NotNull(vm.Solution);
        Assert.False(vm.ServiceVariabilityHasError);
        Assert.Equal(Rho / (2.0 * Mu * (1.0 - Rho)), vm.Solution!.Result.Wq, 10);
    }

    [Fact]
    public void RateWiseAndMeanWise_SamePhysicalInputs_GiveTheSameAnswer()
    {
        var rateWise = new TheoryTabViewModel();
        double byRate = rateWise.Solution!.Result.Wq;

        var meanWise = new TheoryTabViewModel();
        meanWise.SetMode(ParameterMode.MeanWise);
        Assert.Equal("2", meanWise.LambdaInput);
        Assert.Equal("1.25", meanWise.MuInput);
        Assert.Equal(byRate, meanWise.Solution!.Result.Wq, 10);
    }

    [Fact]
    public void UnitChange_RewritesNumbersButKeepsTheAnswer()
    {
        var vm = new TheoryTabViewModel();
        double original = vm.Solution!.Result.Wq;

        vm.SetUnit(TimeUnit.Hours);

        Assert.Equal("30", vm.LambdaInput);
        Assert.Equal("48", vm.MuInput);
        Assert.Equal(original, vm.Solution!.Result.Wq, 10);
    }

    [Fact]
    public void UnitChange_RescalesVarianceByTheSquareOfTheUnit()
    {
        var vm = new TheoryTabViewModel();
        vm.SetModel(TheoryModel.Mg1);
        vm.SetVariabilityMode(VariabilityMode.Variance);
        vm.ServiceVariabilityInput = "1.5625";
        double original = vm.Solution!.Result.Wq;

        vm.SetUnit(TimeUnit.Hours);

        Assert.Equal(original, vm.Solution!.Result.Wq, 10);
        Assert.False(vm.ServiceVariabilityHasError);
    }

    [Fact]
    public void UnstableArrivalRate_IsRefusedWithCause()
    {
        var vm = new TheoryTabViewModel();

        vm.LambdaInput = "1";

        Assert.Null(vm.Solution);
        Assert.Contains("unstable", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ρ", vm.ErrorMessage);
    }

    [Fact]
    public void BlankRate_NamesTheFieldAndDoesNotShowZero()
    {
        var vm = new TheoryTabViewModel();

        vm.MuInput = "";
        vm.ValidateField("mu");

        Assert.Null(vm.Solution);
        Assert.True(vm.MuHasError);
        Assert.Contains("Service rate μ", vm.MuError);
        Assert.DoesNotContain("0.00", vm.Headline);
    }

    [Fact]
    public void InlineError_WaitsForExplicitValidation()
    {
        var vm = new TheoryTabViewModel();

        vm.MuInput = "";

        Assert.False(vm.MuHasError);
        Assert.Null(vm.MuError);
        Assert.Null(vm.Solution);

        vm.ValidateField("mu");

        Assert.True(vm.MuHasError);
        Assert.Contains("Service rate μ", vm.MuError);
    }

    [Fact]
    public void InlineError_ClearsImmediatelyWhenFieldIsCorrected()
    {
        var vm = new TheoryTabViewModel();

        vm.MuInput = "";
        vm.ValidateField("mu");
        Assert.True(vm.MuHasError);

        vm.MuInput = "not-a-number";
        Assert.True(vm.MuHasError, "an existing error must persist while the field is still invalid");

        vm.MuInput = "0.8";
        Assert.False(vm.MuHasError, "the inline error must clear the moment the field becomes valid");
        Assert.Null(vm.MuError);
        Assert.NotNull(vm.Solution);
    }

    private static Window Host(TheoryTab tab, int width = 1200, int height = 760)
    {
        var window = new Window { Width = width, Height = height, Content = tab };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    [AvaloniaFact]
    public void View_UsesLeftQuestionRightAnswerColumns()
    {
        var window = Host(new TheoryTab { DataContext = new TheoryTabViewModel() });
        try
        {
            var grid = window.GetVisualDescendants().OfType<Grid>()
                .First(g => g.ColumnDefinitions.Count == 3
                         && g.ColumnDefinitions[0].Width.GridUnitType == GridUnitType.Pixel);
            Assert.Equal(360d, grid.ColumnDefinitions[0].Width.Value);
            Assert.Equal(GridUnitType.Star, grid.ColumnDefinitions[2].Width.GridUnitType);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void View_ShowsWorkedRowsAndHidesTheApproximationNoticeForMm1()
    {
        var vm = new TheoryTabViewModel();
        var window = Host(new TheoryTab { DataContext = vm });
        try
        {
            var texts = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text).Where(t => t is not null).ToArray();
            Assert.Contains("Worked substitutions", texts);
            Assert.Contains("Wq", texts);
            Assert.Contains("P(wait)", texts);

            var notice = window.GetVisualDescendants().OfType<Border>()
                .Single(b => b.BorderThickness == new Avalonia.Thickness(2));
            Assert.False(notice.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void View_Gg1_ShowsProminentNoticeAndNoPWaitRow()
    {
        var vm = new TheoryTabViewModel();
        vm.SetModel(TheoryModel.Gg1);
        vm.ServiceVariabilityInput = "1.4";
        vm.ArrivalVariabilityInput = "1.2";
        var window = Host(new TheoryTab { DataContext = vm });
        try
        {
            var notice = window.GetVisualDescendants().OfType<Border>()
                .Single(b => b.BorderThickness == new Avalonia.Thickness(2));
            Assert.True(notice.IsEffectivelyVisible, "G/G/1 must show the approximation notice");
            Assert.Contains("not exact in general", notice.GetVisualDescendants().OfType<TextBlock>().Last().Text);

            var texts = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text).Where(t => t is not null).ToArray();
            Assert.DoesNotContain("P(wait)", texts);
            Assert.Contains(texts, t => t!.Contains("arrival epochs"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void View_FormulaSheet_IsCollapsedThenExpands()
    {
        var window = Host(new TheoryTab { DataContext = new TheoryTabViewModel() });
        try
        {
            var sheet = window.GetVisualDescendants().OfType<CollapsibleSection>()
                .Single(s => s.Title?.StartsWith("Formula sheet") == true);
            Assert.False(sheet.IsExpanded);
            Assert.DoesNotContain(
                window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text),
                t => t?.Contains("Little's law") == true);

            sheet.IsExpanded = true;
            window.UpdateLayout();

            Assert.Contains(
                window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text),
                t => t?.Contains("Little's law") == true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void View_ResizingTheWindow_MovesTheWorkedTableWithoutClippingIt()
    {
        var window = Host(new TheoryTab { DataContext = new TheoryTabViewModel() });
        try
        {
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().Last();
            double before = scroller.Bounds.Height;

            window.Width = 1100;
            window.Height = 700;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            double after = scroller.Bounds.Height;
            Assert.True(Math.Abs(after - before) > 1, "the resize never reached the results scroller");

            var table = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(t => t.Text == "Worked substitutions");
            var right = table.TranslatePoint(new Point(table.Bounds.Width, 0), window)!.Value.X;
            Assert.True(
                right <= window.Bounds.Width,
                $"the worked table ends at x={right:F0}, past the {window.Bounds.Width:F0}px window");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void WorkedRows_HaveFullNamesAndSteppedSubstitutions()
    {
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Mm1, Lambda, Mu, 1, 1, out var solution, out _));

        var wq = solution!.Rows.Single(r => r.Symbol == "Wq");
        Assert.Equal("Mean queue waiting time", wq.Name);
        Assert.Contains('\n', wq.Substitution);

        var wait = solution.Rows.Single(r => r.Symbol == "Wait/h");
        Assert.Equal("Aggregate customer wait per hour", wait.Name);
        Assert.Contains("all customers", wait.Substitution, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormulaSheet_PutsEachFormulaOnItsOwnLine()
    {
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Mm1, Lambda, Mu, 1, 1, out var mm1, out _));
        var mmLines = mm1!.FormulaSheetText.Split('\n');
        Assert.Contains(mmLines, l => l.Trim() == "5. Wq = Lq / λ");
        Assert.Contains(mmLines, l => l.Trim() == "6. Wq = ρ / [μ(1 − ρ)]");
        Assert.DoesNotContain(mmLines, l => l.Contains("Wq =") && l.Contains("Lq ="));

        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Mg1, Lambda, Mu, 1, 0, out var mg1, out _));
        var mgLines = mg1!.FormulaSheetText.Split('\n');
        Assert.Contains(mgLines, l => l.Trim() == "5. Lq = λWq");
        Assert.Contains(mgLines, l => l.Trim() == "6. W = Wq + 1/μ");
        Assert.Contains(mgLines, l => l.Trim() == "7. L = λW (Little's law)");
        Assert.DoesNotContain(mgLines, l => l.Contains("Lq =") && l.Contains("W ="));
    }

    [Fact]
    public void PWaitNote_OnlyExplainsTheOmittedGg1Row()
    {
        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Mm1, Lambda, Mu, 1, 1, out var mm1, out _));
        Assert.Equal(string.Empty, mm1!.PWaitNote);

        Assert.True(TheoryWorkedSolutionBuilder.TryBuild(
            TheoryModel.Gg1, Lambda, Mu, 1, 1, out var gg1, out _));
        Assert.Contains("arrival epochs", gg1!.PWaitNote);
    }

    [Fact]
    public void Variability_SigmaAndSquaredCv_MatchCv()
    {
        const double cv = 0.5;
        double mean = 1.0 / Mu;
        double cv2 = cv * cv;

        var vm = new TheoryTabViewModel();
        vm.SetModel(TheoryModel.Mg1);
        vm.SetVariabilityMode(VariabilityMode.Cv);
        vm.ServiceVariabilityInput = "0.5";
        double byCv = vm.Solution!.Result.Wq;

        vm.SetVariabilityMode(VariabilityMode.StandardDeviation);
        string sigmaText = TheoryWorkedSolutionBuilder.VariabilityText(
            cv2, mean, VariabilityMode.StandardDeviation);
        vm.ServiceVariabilityInput = sigmaText;
        double bySigma = vm.Solution!.Result.Wq;

        vm.SetVariabilityMode(VariabilityMode.SquaredCv);
        string squaredCvText = TheoryWorkedSolutionBuilder.VariabilityText(
            cv2, mean, VariabilityMode.SquaredCv);
        vm.ServiceVariabilityInput = squaredCvText;
        double bySquaredCv = vm.Solution!.Result.Wq;

        Assert.Equal(cv * mean, double.Parse(sigmaText, CultureInfo.InvariantCulture), 10);
        Assert.Equal(cv2, double.Parse(squaredCvText, CultureInfo.InvariantCulture), 10);
        Assert.Equal(byCv, bySigma, 10);
        Assert.Equal(byCv, bySquaredCv, 10);
    }

    [Fact]
    public void SpreadHint_ExplainsWhyMm1AndMg1Agree()
    {
        var mm1 = new TheoryTabViewModel();
        Assert.False(mm1.HasSpreadHint);

        var mg1 = new TheoryTabViewModel();
        mg1.SetModel(TheoryModel.Mg1);
        Assert.True(mg1.HasSpreadHint);
        Assert.Contains("M/M/1", mg1.SpreadHint);

        mg1.ServiceVariabilityInput = "0.5";
        Assert.False(mg1.HasSpreadHint);
    }

    [AvaloniaFact]
    public void View_ShowsFullNameColumnAndSpreadHint()
    {
        var vm = new TheoryTabViewModel();
        vm.SetModel(TheoryModel.Mg1);
        var window = Host(new TheoryTab { DataContext = vm });
        try
        {
            var texts = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text).Where(t => t is not null).ToArray();
            Assert.Contains("Name", texts);
            Assert.Contains("Mean queue waiting time", texts);
            Assert.Contains("Aggregate customer wait per hour", texts);

            var hint = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(t => t.Text == vm.SpreadHint);
            Assert.True(hint.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void View_VariabilityControls_DescribeAllFiveEntryForms()
    {
        var window = Host(new TheoryTab { DataContext = new TheoryTabViewModel() });
        try
        {
            var section = window.GetVisualDescendants().OfType<CollapsibleSection>()
                .Single(s => s.Title?.StartsWith("3 · Variability") == true);
            Assert.Contains("five", section.HelpText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("σ", section.HelpText);
            Assert.Contains("C²", section.HelpText);

            var dropdown = window.GetVisualDescendants().OfType<DropdownField>()
                .Single(d => d.Label == "Enter spread as");
            Assert.Contains("σ", dropdown.Placeholder);
            Assert.Contains("C²", dropdown.Placeholder);
            Assert.Contains("five", dropdown.Tooltip, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void View_FullNameColumn_StaysOnScreenAtProductionSize()
    {
        var window = Host(new TheoryTab { DataContext = new TheoryTabViewModel() });
        try
        {
            foreach (string name in new[] { "Mean queue waiting time", "Aggregate customer wait per hour" })
            {
                var cell = window.GetVisualDescendants().OfType<TextBlock>()
                    .Single(t => t.Text == name);
                var right = cell.TranslatePoint(new Point(cell.Bounds.Width, 0), window)!.Value.X;
                Assert.True(
                    right <= window.Bounds.Width,
                    $"the Name cell \"{name}\" ends at x={right:F0}, past the {window.Bounds.Width:F0}px window");
            }
        }
        finally
        {
            window.Close();
        }
    }
}
