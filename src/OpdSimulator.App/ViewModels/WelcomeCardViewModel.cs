namespace OpdSimulator.App.ViewModels;

using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using OpdSimulator.App;

/// <summary>
/// Welcome card content (FR-UI-5 / AGENTS §16.6): the two logos, the course
/// identity and the member list, all read from the single
/// <see cref="CourseInfo"/> source so the XAML never carries course details.
/// Logos are resolved eagerly so a missing asset logs a warning instead of
/// crashing at render time (the card degrades to text alone).
/// </summary>
public class WelcomeCardViewModel : ViewModelBase
{
    private static readonly Func<string, Bitmap?> BitmapFromAvares = path =>
    {
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(new Uri(path));
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Welcome logo could not be loaded: {Path}", path);
            return null;
        }
    };

    /// <summary>Gets the University of Karachi logo, or null when unavailable.</summary>
    public Bitmap? UokLogoImage => BitmapFromAvares(CourseInfo.UokLogoPath);

    /// <summary>Gets the UBIT/DCS logo, or null when unavailable.</summary>
    public Bitmap? UbitLogoImage => BitmapFromAvares(CourseInfo.UbitLogoPath);

    /// <summary>Gets the full course name.</summary>
    public string CourseName => CourseInfo.CourseName;

    /// <summary>Gets the course code shown under the course name.</summary>
    public string CourseCode => CourseInfo.CourseCode;

    /// <summary>Gets the instructor's name.</summary>
    public string Professor => CourseInfo.Professor;

    /// <summary>Gets the group members, one per line.</summary>
    public IReadOnlyList<string> Members => CourseInfo.Members;
}