using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Views;

/// <summary>
/// Code-behind for <see cref="GuidePanel"/>: projects the parsed
/// <see cref="GuideBlock"/>s of the selected section onto theme-styled Avalonia
/// controls (block-level XAML templates cannot honour inline bold/code runs).
/// </summary>
public partial class GuidePanel : UserControl
{
    /// <summary>Creates the panel.</summary>
    public GuidePanel()
    {
        InitializeComponent();
    }

    /// <summary>Moves focus to the search box when the guide opens (keyboard contract).</summary>
    public void FocusSearch() => SearchBox?.Focus();

    /// <summary>Subscribes to selection changes so the body re-renders.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is GuideViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(GuideViewModel.Selected))
                {
                    Dispatcher.UIThread.Post(() => Render(vm.Selected));
                }
            };
            Dispatcher.UIThread.Post(() => Render(vm.Selected));
        }
    }

    private void Render(GuideSection? section)
    {
        if (Body is null || section is null)
        {
            return;
        }
        Body.Children.Clear();

        foreach (var block in section.Blocks)
        {
            Body.Children.Add(block.Kind switch
            {
                GuideBlockKind.Heading => Heading(block),
                GuideBlockKind.Paragraph => Paragraph(block.Runs, null),
                GuideBlockKind.Bullet => Paragraph(block.Runs, "•  "),
                GuideBlockKind.Numbered => Paragraph(block.Runs, null),
                GuideBlockKind.Code => Code(block.Text),
                _ => new Separator { Margin = new Thickness(0, 4, 0, 4) },
            });
        }
    }

    private static TextBlock Heading(GuideBlock block)
        => new()
        {
            Text = string.Concat(block.Runs.Select(r => r.Text)),
            FontSize = block.Level switch { 1 => 20, 2 => 17, _ => 14 },
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, block.Level >= 2 ? 6 : 0, 0, 0),
        };

    private static TextBlock Paragraph(IReadOnlyList<InlineRun> runs, string? prefix)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        if (prefix is not null)
        {
            text.Inlines!.Add(new Run(prefix));
        }
        foreach (var run in runs)
        {
            if (run.Style == InlineStyle.Bold)
            {
                text.Inlines!.Add(new Run(run.Text) { FontWeight = FontWeight.SemiBold });
            }
            else if (run.Style == InlineStyle.Code)
            {
                var code = new Run(run.Text) { FontFamily = new FontFamily("Consolas, Courier New, monospace") };
                text.Inlines!.Add(code);
            }
            else
            {
                text.Inlines!.Add(new Run(run.Text));
            }
        }
        return text;
    }

    private Border Code(string text)
        => new()
        {
            Background = Avalonia.Application.Current?.Resources.TryGetResource("BrushBackgroundAlt", null, out var bg) == true && bg is IBrush brush ? brush : null,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            },
        };
}