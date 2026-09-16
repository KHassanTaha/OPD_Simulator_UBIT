namespace OpdSimulator.App;

/// <summary>
/// Single source of truth for the course details shown on the welcome card
/// (AGENTS §16.6). Keeping these in one constants file means a course,
/// member or professor change touches exactly one place — no XAML edits.
/// </summary>
public static class CourseInfo
{
    /// <summary>Full course name shown on the welcome card.</summary>
    public const string CourseName = "Simulation & Modelling";

    /// <summary>Course code as it appears on the transcript (CS-577).</summary>
    public const string CourseCode = "CS-577";

    /// <summary>Instructor name, read directly on the welcome card.</summary>
    public const string Professor = "Dr. Shaista Rais";

    /// <summary>
    /// Group members, in display order. Updated by the project lead; the
    /// welcome card renders each name as its own line.
    /// </summary>
    public static readonly string[] Members =
    {
        "Taha Hassan Khan",
        "Anas Shoaib",
        "Hamza Wahaj",
        "M. Shayan Ghouri",
        "Zayan Ali",
        "Yahya Arif Butt",
    };

    /// <summary>
    /// University of Karachi logo, embedded as an Avalonia resource. Referenced
    /// by URI so a future higher-resolution image replaces only this path.
    /// </summary>
    public const string UokLogoPath = "avares://OpdSimulator.App/Assets/uok-logo.png";

    /// <summary>UBIT/DCS logo, same resource-URI pattern as <see cref="UokLogoPath"/>.</summary>
    public const string UbitLogoPath = "avares://OpdSimulator.App/Assets/ubit-cs-logo.png";
}