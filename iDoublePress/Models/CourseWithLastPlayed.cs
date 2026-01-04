using iDoublePress.Resources.Strings;

namespace iDoublePress.Models;

/// <summary>
/// Wrapper class to hold a Course and its last played date.
/// </summary>
public sealed class CourseWithLastPlayed
{
    public Course Course { get; }
    public DateTime? LastPlayed { get; }

    public string Name => Course.Name;
    public int TotalPar => Course.TotalPar;
    public bool HasLastPlayed => LastPlayed.HasValue;
    public string LastPlayedDisplay => LastPlayed.HasValue
        ? string.Format(AppResources.LastPlayedFormat, LastPlayed.Value.ToString("MMM d, yyyy"))
        : string.Empty;

    public CourseWithLastPlayed(Course course, DateTime? lastPlayed)
    {
        Course = course;
        LastPlayed = lastPlayed;
    }
}
