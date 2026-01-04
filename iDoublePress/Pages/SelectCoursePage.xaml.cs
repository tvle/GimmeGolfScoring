using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using iDoublePress.Models;

namespace iDoublePress.Pages;

public partial class SelectCoursePage : ContentPage
{
    private readonly TaskCompletionSource<Course?> _tcs = new();
    private readonly SelectCoursePageModel _viewModel;

    public SelectCoursePage(List<Course> courses, List<Round> rounds)
    {
        InitializeComponent();
        _viewModel = new SelectCoursePageModel(courses, rounds);
        BindingContext = _viewModel;
    }

    public Task<Course?> GetResultAsync() => _tcs.Task;

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    private async void OnCourseTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not CourseWithLastPlayed item)
            return;

        _tcs.TrySetResult(item.Course);
        await Navigation.PopModalAsync();
    }

    private void OnTabSelectionChanged(object? sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
    {
        var isRecentTab = e.NewIndex == 0;
        RecentCoursesView.IsVisible = isRecentTab;
        AllCoursesView.IsVisible = !isRecentTab;
    }

    private void OnSortAscClicked(object? sender, EventArgs e)
    {
        _viewModel.SortByNameAsc();
    }

    private void OnSortDescClicked(object? sender, EventArgs e)
    {
        _viewModel.SortByNameDesc();
    }
}

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

internal sealed class SelectCoursePageModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public SelectCoursePageModel(List<Course> courses, List<Round> rounds)
    {
        // Build a dictionary of course ID to last played date
        var lastPlayedByCourse = rounds
            .Where(r => r.Course != null)
            .GroupBy(r => r.CourseID)
            .ToDictionary(g => g.Key, g => g.Max(r => r.StartTime));

        // All courses list with last played dates
        var allCoursesWithLastPlayed = courses
            .Select(c => new CourseWithLastPlayed(c, lastPlayedByCourse.TryGetValue(c.ID, out var date) ? date : null))
            .ToList();
        AllCourses = new ObservableCollection<CourseWithLastPlayed>(allCoursesWithLastPlayed);

        // Recent courses - get unique courses from rounds sorted by most recent play date
        var recentCourses = rounds
            .Where(r => r.Course != null)
            .OrderByDescending(r => r.StartTime)
            .GroupBy(r => r.CourseID)
            .Select(g => new CourseWithLastPlayed(g.First().Course!, g.Max(r => r.StartTime)))
            .ToList();

        RecentCourses = new ObservableCollection<CourseWithLastPlayed>(recentCourses);
    }

    public ObservableCollection<CourseWithLastPlayed> AllCourses { get; }
    public ObservableCollection<CourseWithLastPlayed> RecentCourses { get; }

    public void SortByNameAsc()
    {
        var sorted = AllCourses
            .OrderBy(c => c.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        AllCourses.Clear();
        foreach (var c in sorted)
            AllCourses.Add(c);
    }

    public void SortByNameDesc()
    {
        var sorted = AllCourses
            .OrderByDescending(c => c.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        AllCourses.Clear();
        foreach (var c in sorted)
            AllCourses.Add(c);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
