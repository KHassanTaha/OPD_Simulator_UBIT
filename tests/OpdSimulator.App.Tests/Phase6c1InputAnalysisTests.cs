using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.1 gate tests (feat/milestone-6c-input-analysis-charts): the new
/// <see cref="ChartCard"/> reusable control and the Input Analysis tab scaffold.
/// The card must render Title + Caption, show the themed empty state while it
/// has no data, and hide it the moment data is present. Assertions target the
/// two container areas inside the card template (EmptyStateBorder / ChartHost)
/// whose IsVisible is driven by the single ShowEmptyState toggle — plus the
/// rendered text — never existence-only (AGENTS §9/§16).
/// </summary>
/// <remarks>
/// Naming quirk learnt here: Avalonia's <c>IsVisible</c> is local, not
/// effective — a text block under a hidden <see cref="Border"/> still reports
/// <c>IsVisible = true</c> — and a hidden <see cref="ContentPresenter"/> has its
/// content removed from the visual tree entirely. Asserting the container
/// visibility avoids both traps and reads the control's actual contract.
/// </remarks>
public class Phase6c1InputAnalysisTests
{
    private const string EmptyText = "No data yet.";

    [AvaloniaFact]
    public void ChartCard_RendersTitleAndCaption()
    {
        var card = new ChartCard
        {
            Title = "Inter-arrival time",
            Caption = "Exponential fit, λ = 0.500",
            ShowEmptyState = false,
            ChartContent = new TextBlock { Text = "Histogram body" },
        };

        InHost(card, window =>
        {
            Assert.True(FindTextBlock(card, "Inter-arrival time").IsVisible,
                "the card title must render");
            Assert.True(FindTextBlock(card, "Exponential fit, λ = 0.500").IsVisible,
                "the card caption must render when provided");
            Assert.True(FindTextBlock(card, "Histogram body").IsVisible,
                "the chart content must render once data is present");
            Assert.False(Part(card, "EmptyStateBorder").IsVisible,
                "the empty state must not show while the card has data");
        });
    }

    [AvaloniaFact]
    public void ChartCard_EmptyStateVisibleWhenNoData()
    {
        // Freshly-created cards default to ShowEmptyState = true.
        var card = new ChartCard
        {
            Title = "Waiting-time histogram",
            EmptyStateText = EmptyText,
            ChartContent = new TextBlock { Text = "CHART" },
        };

        Assert.True(card.ShowEmptyState, "a fresh card must default to the empty state");

        InHost(card, window =>
        {
            Assert.True(Part(card, "EmptyStateBorder").IsVisible,
                "the empty-state box must show while the card has no data");
            Assert.False(Part(card, "ChartHost").IsVisible,
                "the chart host must be hidden while the empty state shows");
            Assert.True(FindTextBlock(card, EmptyText).IsVisible,
                "the empty-state message must render while the card has no data");
            Assert.Empty(card.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Text == "CHART"));
        });
    }

    [AvaloniaFact]
    public void ChartCard_HiddenEmptyState_WhenDataPresent()
    {
        var card = new ChartCard
        {
            Title = "Waiting-time histogram",
            EmptyStateText = EmptyText,
            ShowEmptyState = false,
            ChartContent = new TextBlock { Text = "CHART" },
        };

        InHost(card, window =>
        {
            Assert.True(Part(card, "ChartHost").IsVisible,
                "the chart host must render once data is present");
            Assert.True(FindTextBlock(card, "CHART").IsVisible,
                "the chart content must render once data is present");
            Assert.False(Part(card, "EmptyStateBorder").IsVisible,
                "the empty-state box must be hidden while the chart shows");
        });
    }

    [AvaloniaFact]
    public void InputAnalysisView_ShowsEmptyMessage_WhenNoFileLoaded()
    {
        var vm = new InputAnalysisViewModel();
        Assert.True(vm.IsEmpty, "a fresh input-analysis view model must be empty");

        var view = new InputAnalysisView { DataContext = vm };
        InHost(view, window =>
        {
            var message = FindTextBlock(view, vm.EmptyMessage);
            Assert.True(message.IsVisible,
                "the tab must show its empty prompt when no file is loaded");
            Assert.Equal(vm.EmptyMessage, message.Text);
            Assert.True(FindTextBlock(view, "Input analysis").IsVisible,
                "the tab heading must render in the empty state");
        });
    }

    private static IOrderedEnumerable<TextBlock> RenderedTexts(Control root)
        => root.GetVisualDescendants().OfType<TextBlock>().OrderBy(t => t.Text);

    private static TextBlock FindTextBlock(Control root, string text)
        => RenderedTexts(root).FirstOrDefault(t => t.Text == text)
           ?? throw new Xunit.Sdk.XunitException($"no TextBlock '{text}' in visual tree");

    private static Control Part(Control root, string name)
        => root.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name)
           ?? throw new Xunit.Sdk.XunitException($"no named part '{name}' in visual tree");

    private static void InHost(Control content, Action<Window> body)
    {
        var window = new Window { Width = 500, Height = 400, Content = content };
        window.Show();
        window.UpdateLayout();
        try
        {
            body(window);
        }
        finally
        {
            window.Close();
        }
    }
}