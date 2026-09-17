using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 7B gate tests (feat/milestone-7-model-driven): the Kendall-notation
/// parser and the per-stage model dropdown / Advanced distribution selectors.
/// Verification intent: a model shortcut must translate "A/S/c" into the
/// arrival family, service family and server count; Advanced mode must stop
/// that sync and instead expose two independent family dropdowns; every stage
/// row must offer the shortcut. The wiring into the run parameters (first
/// stage's families) is covered by the existing run-flow suite.
/// </summary>
public class Phase7BTests
{
    private static readonly string[] ExpectedModels =
    {
        "M/M/1", "M/M/2", "M/M/3", "M/M/4", "M/M/5",
        "M/D/1", "M/D/2", "M/D/3",
        "D/M/1", "D/M/2",
        "G/G/1",
    };

    // ── Parser ──────────────────────────────────────────────────────────

    [Fact]
    public void Parser_MM1_ReturnsExponentialExponential1()
    {
        var parsed = ModelNotationParser.Parse("M/M/1");

        Assert.Equal("Exponential", parsed.ArrivalFamily);
        Assert.Equal("Exponential", parsed.ServiceFamily);
        Assert.Equal(1, parsed.ServerCount);
    }

    [Fact]
    public void Parser_MM3_ReturnsExponentialExponential3()
    {
        var parsed = ModelNotationParser.Parse("M/M/3");

        Assert.Equal("Exponential", parsed.ArrivalFamily);
        Assert.Equal("Exponential", parsed.ServiceFamily);
        Assert.Equal(3, parsed.ServerCount);
    }

    [Fact]
    public void Parser_MD2_ReturnsExponentialDeterministic2()
    {
        var parsed = ModelNotationParser.Parse("M/D/2");

        Assert.Equal("Exponential", parsed.ArrivalFamily);
        Assert.Equal("Deterministic", parsed.ServiceFamily);
        Assert.Equal(2, parsed.ServerCount);
    }

    [Fact]
    public void Parser_DM1_ReturnsDeterministicExponential1()
    {
        var parsed = ModelNotationParser.Parse("D/M/1");

        Assert.Equal("Deterministic", parsed.ArrivalFamily);
        Assert.Equal("Exponential", parsed.ServiceFamily);
        Assert.Equal(1, parsed.ServerCount);
    }

    [Fact]
    public void Parser_GG1_ReturnsGeneralGeneral1()
    {
        var parsed = ModelNotationParser.Parse("G/G/1");

        Assert.Equal("General", parsed.ArrivalFamily);
        Assert.Equal("General", parsed.ServiceFamily);
        Assert.Equal(1, parsed.ServerCount);
    }

    [Fact]
    public void Parser_Invalid_MissingSlash_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => ModelNotationParser.Parse("MM1"));
        Assert.Contains("Invalid model notation 'MM1'", ex.Message);
    }

    [Fact]
    public void Parser_Invalid_BadServerCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => ModelNotationParser.Parse("M/M/0"));
        Assert.Throws<ArgumentException>(() => ModelNotationParser.Parse("M/M/6"));
        Assert.Throws<ArgumentException>(() => ModelNotationParser.Parse("M/M/x"));
    }

    [Fact]
    public void Parser_Invalid_UnknownLetter_Throws()
    {
        Assert.Throws<ArgumentException>(() => ModelNotationParser.Parse("X/M/1"));
        Assert.Throws<ArgumentException>(() => ModelNotationParser.Parse("M/Z/1"));
    }

    [Fact]
    public void Parser_StandardModels_HasExactly11Entries()
    {
        Assert.Equal(11, ModelNotationParser.StandardModels.Count);
        Assert.Equal(ExpectedModels, ModelNotationParser.StandardModels);
    }

    [Fact]
    public void Parser_StandardModels_AllParse()
    {
        foreach (var model in ModelNotationParser.StandardModels)
        {
            var parsed = ModelNotationParser.Parse(model);
            Assert.InRange(parsed.ServerCount, 1, 5);
        }
    }

    // ── StageRow ────────────────────────────────────────────────────────

    [Fact]
    public void StageRow_DefaultModel_IsMM1()
    {
        var row = new StageRow();

        Assert.Equal("M/M/1", row.SelectedModel);
        Assert.Equal("Exponential", row.ArrivalFamily);
        Assert.Equal("Exponential", row.ServiceFamily);
        Assert.Equal("1", row.Servers.Value);
    }

    [Fact]
    public void StageRow_ChangeModel_FillsArrivalFamily()
    {
        var row = new StageRow { SelectedModel = "D/M/2" };

        Assert.Equal("Deterministic", row.ArrivalFamily);
    }

    [Fact]
    public void StageRow_ChangeModel_FillsServiceFamily()
    {
        var row = new StageRow { SelectedModel = "M/D/2" };

        Assert.Equal("Deterministic", row.ServiceFamily);
    }

    [Fact]
    public void StageRow_ChangeModel_UpdatesServerCount()
    {
        var row = new StageRow { SelectedModel = "M/M/4" };

        Assert.Equal("4", row.Servers.Value);
    }

    [Fact]
    public void StageRow_AdvancedMode_SkipsModelSync()
    {
        var row = new StageRow { UseAdvancedSetup = true, SelectedModel = "M/M/5" };

        Assert.Equal("M/M/5", row.SelectedModel);
        Assert.Equal("Exponential", row.ArrivalFamily);
        Assert.Equal("Exponential", row.ServiceFamily);
        Assert.Equal("1", row.Servers.Value);
    }

    // ── ConfigPanel wiring ──────────────────────────────────────────────

    [AvaloniaFact]
    public void ConfigPanel_EachStage_HasModelDropdown()
    {
        var vm = new ConfigPanelViewModel();
        var panel = new ConfigPanel { DataContext = vm };
        var window = Host(panel);

        try
        {
            Assert.Equal(3, vm.StageRows.Count);

            var modelDropdowns = Dropdowns(window, "Model");
            Assert.Equal(3, modelDropdowns.Count);
            Assert.All(modelDropdowns, d => Assert.True(d.IsVisible));
            Assert.All(modelDropdowns, d =>
                Assert.Equal(ExpectedModels, d.Items!));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ConfigPanel_AdvancedToggle_RevealsTwoDistributions()
    {
        var vm = new ConfigPanelViewModel();
        var panel = new ConfigPanel { DataContext = vm };
        var window = Host(panel);

        try
        {
            var model = Dropdowns(window, "Model");
            var arrival = Dropdowns(window, "Arrival distribution");
            var service = Dropdowns(window, "Service distribution");

            Assert.Equal(3, model.Count);
            Assert.Equal(3, arrival.Count);
            Assert.Equal(3, service.Count);
            Assert.All(arrival, d => Assert.False(d.IsVisible));
            Assert.All(service, d => Assert.False(d.IsVisible));

            // Drive the real control (row 0's toggle) — it is bound TwoWay to
            // StageRows[0].UseAdvancedSetup.
            var advanced = window.GetVisualDescendants().OfType<ToggleSwitch>()
                .First(t => Equals(t.Content, "Advanced"));
            advanced.IsChecked = true;
            window.UpdateLayout();

            Assert.Equal(2, model.Count(d => d.IsVisible));
            Assert.Equal(1, arrival.Count(d => d.IsVisible));
            Assert.Equal(1, service.Count(d => d.IsVisible));

            advanced.IsChecked = false;
            window.UpdateLayout();

            Assert.Equal(3, model.Count(d => d.IsVisible));
            Assert.Equal(0, arrival.Count(d => d.IsVisible));
            Assert.Equal(0, service.Count(d => d.IsVisible));
        }
        finally
        {
            window.Close();
        }
    }

    private static List<SearchableDropdown> Dropdowns(Window window, string label)
        // Scope to the stage rows: the Model section has its own
        // "Service distribution" dropdown, so label alone is ambiguous.
        => window.GetVisualDescendants().OfType<SearchableDropdown>()
            .Where(d => d.Label == label && d.DataContext is StageRow)
            .ToList();

    private static Window Host(ConfigPanel panel, int width = 900, int height = 1400)
    {
        var window = new Window { Width = width, Height = height, Content = panel };
        window.Show();
        window.UpdateLayout();
        return window;
    }
}
