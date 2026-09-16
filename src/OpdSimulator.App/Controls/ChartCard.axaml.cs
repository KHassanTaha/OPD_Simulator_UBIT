using Avalonia;
using Avalonia.Controls;

namespace OpdSimulator.App.Controls;

/// <summary>
/// One themed card that hosts a single chart (Phase 6C). Reusable by every
/// Input-Analysis and results chart: a Title, an optional Caption, the chart
/// control itself (<see cref="ChartContent"/>), and a themed empty-state text
/// shown until data exists. All visuals come from theme tokens.
/// </summary>
/// <remarks>
/// <see cref="ShowEmptyState"/> is the single source of truth for which area
/// renders: when true the empty-state box is visible and the chart host is
/// collapsed, and vice-versa. The two visibility switches are driven by one
/// property so the card can never show both (or neither).
/// </remarks>
public partial class ChartCard : UserControl
{
    /// <summary>
    /// Heading of the card (e.g. "Inter-arrival time").
    /// </summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ChartCard, string?>(nameof(Title));

    /// <summary>
    /// Optional sub-line under the title (e.g. the chi-square verdict).
    /// Hidden when blank.
    /// </summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<ChartCard, string?>(nameof(Caption));

    /// <summary>
    /// Message shown in the themed placeholder box while the card has no data.
    /// </summary>
    public static readonly StyledProperty<string?> EmptyStateTextProperty =
        AvaloniaProperty.Register<ChartCard, string?>(nameof(EmptyStateText));

    /// <summary>
    /// True to show the empty-state box and hide the chart host; false to
    /// show the chart. Defaults to true so a freshly-created card reads as
    /// "no data yet".
    /// </summary>
    public static readonly StyledProperty<bool> ShowEmptyStateProperty =
        AvaloniaProperty.Register<ChartCard, bool>(nameof(ShowEmptyState), true);

    /// <summary>
    /// The LiveCharts control (or any control) this card presents.
    /// </summary>
    public static readonly StyledProperty<object?> ChartContentProperty =
        AvaloniaProperty.Register<ChartCard, object?>(nameof(ChartContent));

    static ChartCard()
    {
        ShowEmptyStateProperty.Changed.AddClassHandler<ChartCard>((card, _) => card.ApplyEmptyState());
        CaptionProperty.Changed.AddClassHandler<ChartCard>((card, _) => card.ApplyCaptionVisibility());
    }

    public ChartCard()
    {
        InitializeComponent();
        ApplyEmptyState();
        ApplyCaptionVisibility();
    }

    /// <summary>Heading of the card.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Optional sub-line under the title; hidden when blank.</summary>
    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>Message shown in the themed placeholder box while the card has no data.</summary>
    public string? EmptyStateText
    {
        get => GetValue(EmptyStateTextProperty);
        set => SetValue(EmptyStateTextProperty, value);
    }

    /// <summary>True shows the empty-state box; false shows the chart host.</summary>
    public bool ShowEmptyState
    {
        get => GetValue(ShowEmptyStateProperty);
        set => SetValue(ShowEmptyStateProperty, value);
    }

    /// <summary>The chart control this card presents.</summary>
    public object? ChartContent
    {
        get => GetValue(ChartContentProperty);
        set => SetValue(ChartContentProperty, value);
    }

    private void ApplyEmptyState()
    {
        if (EmptyStateBorder is null || ChartHost is null)
        {
            return;
        }

        EmptyStateBorder.IsVisible = ShowEmptyState;
        ChartHost.IsVisible = !ShowEmptyState;
    }

    private void ApplyCaptionVisibility()
    {
        CaptionText.IsVisible = !string.IsNullOrWhiteSpace(Caption);
    }
}