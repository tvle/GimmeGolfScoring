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
        ? $"Last played: {LastPlayed.Value:MMM d, yyyy}"
        : string.Empty;

    public CourseWithLastPlayed(Course course, DateTime? lastPlayed)
    {
        Course = course;
        LastPlayed = lastPlayed;
    }
}
