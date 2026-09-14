namespace OpdSimulator.App.ViewModels;

using System.Collections.ObjectModel;
using System.Globalization;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OpdSimulator.App.Services;
using SkiaSharp;

/// <summary>
/// One chart ready for LiveCharts2: a title plus the series and axes the
/// <c>CartesianChart</c> control binds to. Built from the pure
/// <see cref="ChartsData"/> records so the conversion (histogram bins to
/// column series, fitted density to a line overlay) is a plain projection.
/// </summary>
public sealed class ChartViewModel : ViewModelBase
{
    private ChartViewModel(bool hasData, string title, IEnumerable<ISeries> series, IEnumerable<Axis> xAxes, IEnumerable<Axis> yAxes)
    {
        HasData = hasData;
        Title = title;
        Series = new ObservableCollection<ISeries>(series);
        XAxes = new ObservableCollection<Axis>(xAxes);
        YAxes = new ObservableCollection<Axis>(yAxes);
    }

    /// <summary>Gets whether the chart has anything to draw.</summary>
    public bool HasData { get; }

    /// <summary>Gets the inverse of <see cref="HasData"/>, for the empty placeholder. Stable — never changes after construction.</summary>
    public bool ShowEmpty => !HasData;

    /// <summary>Gets the chart title shown above the plot.</summary>
    public string Title { get; }

    /// <summary>Gets the drawable series (columns and/or lines).</summary>
    public ObservableCollection<ISeries> Series { get; }

    /// <summary>Gets the X axis (category labels or a time axis).</summary>
    public ObservableCollection<Axis> XAxes { get; }

    /// <summary>Gets the Y axis.</summary>
    public ObservableCollection<Axis> YAxes { get; }

    /// <summary>
    /// Builds a categorical chart: one column series per source series, plus a
    /// line overlay when a fitted (continuous) series is present.
    /// </summary>
    public static ChartViewModel FromCategorical(CategoricalChartData data, SKColor primary, SKColor accent)
    {
        if (data.Series.Count == 0)
        {
            return Empty(data.Title);
        }

        var series = new List<ISeries>();
        foreach (var s in data.Series)
        {
            var column = new ColumnSeries<double>
            {
                Name = s.Name,
                Values = s.Values,
                Fill = new SolidColorPaint(s.Name == "Expected" || s.Name == "Fitted distribution" ? accent : primary),
                MaxBarWidth = 22,
            };
            series.Add(column);
        }

        // A fitted distribution is drawn as a line over the observed columns.
        if (data.Series.Any(s => s.Name == "Fitted distribution"))
        {
            var fitted = data.Series.First(s => s.Name == "Fitted distribution");
            series.Add(new LineSeries<double>
            {
                Name = fitted.Name,
                Values = fitted.Values,
                Stroke = new SolidColorPaint(accent, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
            });
        }

        return new ChartViewModel(
            true,
            data.Title,
            series,
            new[] { new Axis { Labels = data.Categories.ToArray() } },
            new[] { DefaultYAxis() });
    }

    /// <summary>Builds an X/Y line chart (e.g. queue length over time per stage).</summary>
    public static ChartViewModel FromLine(LineChartData data, SKColor primary)
    {
        if (data.X.Count == 0)
        {
            return Empty(data.Title);
        }

        var line = new LineSeries<double>
        {
            Name = data.SeriesName,
            Values = data.Y,
            Stroke = new SolidColorPaint(primary, 2),
            Fill = null,
            GeometrySize = 0,
            LineSmoothness = 0,
        };

        return new ChartViewModel(
            true,
            data.Title,
            new[] { line },
            new[] { new Axis { Labeler = v => v.ToString("0") } },
            new[] { DefaultYAxis() });
    }

    /// <summary>An empty-placed holder so the tab keeps a stable item count.</summary>
    public static ChartViewModel Empty(string title)
        => new(false, title, Array.Empty<ISeries>(), new[] { DefaultYAxis() }, new[] { DefaultYAxis() });

    private static Axis DefaultYAxis()
        => new() { Labeler = v => v.ToString("0.##", CultureInfo.InvariantCulture) };
}

/// <summary>The two chart tabs of the results panel (FR-UI-4 P1/P2).</summary>
public sealed class ChartsPanelViewModel : ViewModelBase
{
    private readonly SKColor _primary;

    /// <summary>Gets the charts presented on the "Input analysis" tab.</summary>
    public ObservableCollection<ChartViewModel> InputCharts { get; } = new();

    /// <summary>Gets the charts presented on the "Simulation run" tab.</summary>
    public ObservableCollection<ChartViewModel> RunCharts { get; } = new();

    /// <summary>Gets whether the run dropped any chart data.</summary>
    public bool HasCharts => InputCharts.Count > 0 || RunCharts.Count > 0;

    /// <summary>Gets the inverse of <see cref="HasCharts"/> for the empty state overlay.</summary>
    public bool ShowEmpty => !HasCharts;

    /// <summary>Creates the panel with the theme-derived chart colours.</summary>
    public ChartsPanelViewModel(SKColor primary, SKColor accent)
    {
        _primary = primary;
        _accent = accent;
    }

    /// <summary>
    /// Creates the panel with the palette colours resolved from Theme.axaml
    /// (the single source of colours per AGENTS §16.3), falling back to the
    /// palette values when running outside an initialised application (tests).
    /// </summary>
    public static ChartsPanelViewModel ForResources()
        => new(ResolveColor("ColorBrandGreen", new SKColor(0x1B, 0x7A, 0x4C)),
            ResolveColor("ColorAccentInfo", new SKColor(0x0B, 0x72, 0x85)));

    private static SKColor ResolveColor(string key, SKColor fallback)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var value) == true
            && value is Avalonia.Media.Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }
        return fallback;
    }

    private readonly SKColor _accent;

    /// <summary>Rebuilds every chart from the last run's data.</summary>
    public void SetCharts(ChartsData? data)
    {
        InputCharts.Clear();
        RunCharts.Clear();

        if (data is not null)
        {
            InputCharts.Add(ChartViewModel.FromCategorical(data.InterArrivalHistogram, _primary, _accent));
            foreach (var service in data.ServiceHistograms)
            {
                InputCharts.Add(ChartViewModel.FromCategorical(service, _primary, _accent));
            }
            InputCharts.Add(ChartViewModel.FromCategorical(data.ChiSquare, _primary, _accent));
            InputCharts.Add(ChartViewModel.FromCategorical(data.Utilisation, _primary, _accent));

            foreach (var line in data.QueueOverTime)
            {
                RunCharts.Add(ChartViewModel.FromLine(line, _primary));
            }
            foreach (var waiting in data.WaitingHistograms)
            {
                RunCharts.Add(ChartViewModel.FromCategorical(waiting, _primary, _accent));
            }
        }

        OnPropertyChanged(nameof(HasCharts));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    /// <summary>Clears both tabs (reset).</summary>
    public void Clear()
    {
        InputCharts.Clear();
        RunCharts.Clear();
        OnPropertyChanged(nameof(HasCharts));
        OnPropertyChanged(nameof(ShowEmpty));
    }
}