using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;

namespace iDoublePress.PageModels;

public partial class CoursesPageModel : ObservableObject
{
    private readonly CourseRepository _courseRepository;
    private readonly RoundRepository _roundRepository;

    public ObservableCollection<CourseWithLastPlayed> Courses { get; } = new();

    public int CourseCount => Courses.Count;

    public string Title => string.Format(AppResources.CoursesCountFormat, CourseCount);

    [ObservableProperty]
    private CourseWithLastPlayed? selectedCourse;

    [ObservableProperty]
    private bool isBusy;

    // Tracks the current sort direction: true = ascending, false = descending, null = no explicit sort
    private bool? _isSortAscending;

    public CoursesPageModel(CourseRepository courseRepository, RoundRepository roundRepository)
    {
        _courseRepository = courseRepository;
        _roundRepository = roundRepository;
        Courses.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(CourseCount));
            OnPropertyChanged(nameof(Title));
        };
    }

    [RelayCommand]
    private async Task NavigatedToAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            Courses.Clear();
            
            var courses = await _courseRepository.ListAsync();
            var rounds = await _roundRepository.ListAsync();
            
            // Build a dictionary of course ID to last played date
            var lastPlayedByCourse = rounds
                .Where(r => r.Course != null)
                .GroupBy(r => r.CourseID)
                .ToDictionary(g => g.Key, g => g.Max(r => r.StartTime));
            
            foreach (var c in courses)
            {
                var lastPlayed = lastPlayedByCourse.TryGetValue(c.ID, out var date) ? date : (DateTime?)null;
                Courses.Add(new CourseWithLastPlayed(c, lastPlayed));
            }

            // Re-apply any previously selected sort so ordering is preserved when returning
            if (_isSortAscending.HasValue)
                ApplySort();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenCourseAsync(CourseWithLastPlayed? courseWithLastPlayed)
    {
        if (courseWithLastPlayed is null)
            return;

        SelectedCourse = null;
        await Shell.Current.GoToAsync($"course-edit?courseId={courseWithLastPlayed.Course.ID}");
    }

    [RelayCommand]
    private async Task EditCourseAsync(CourseWithLastPlayed? courseWithLastPlayed)
    {
        await OpenCourseAsync(courseWithLastPlayed);
    }

    [RelayCommand]
    private async Task AddCourseAsync()
    {
        await Shell.Current.GoToAsync("course-edit");
    }

    private string GetNextCopyName(string baseName)
    {
        // Creates names like:
        // "Pebble Beach (1)", "Pebble Beach (2)", ...
        // Ensures uniqueness against the currently loaded list.
        var prefix = $"{baseName} (";

        var max = 0;
        foreach (var c in Courses)
        {
            if (string.IsNullOrWhiteSpace(c.Name))
                continue;

            if (!c.Name.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var start = prefix.Length;
            var end = c.Name.IndexOf(')', start);
            if (end < 0)
                continue;

            var numberText = c.Name.Substring(start, end - start);
            if (int.TryParse(numberText, out var n))
                max = Math.Max(max, n);
        }

        return $"{baseName} ({max + 1})";
    }

    [RelayCommand]
    private async Task CopyCourseAsync(CourseWithLastPlayed? courseWithLastPlayed)
    {
        if (courseWithLastPlayed is null)
            return;

        var course = courseWithLastPlayed.Course;

        // Load a fresh copy including holes, then insert as a new item.
        var source = await _courseRepository.GetAsync(course.ID);
        if (source is null)
            return;

        var copied = new Course
        {
            ID = 0,
            Name = GetNextCopyName(source.Name),
            Location = source.Location,
            TotalPar = source.TotalPar,
            Holes = source.Holes,
            Rating = source.Rating,
            Slope = source.Slope,
            IsCustom = true,
            CourseHoles = source.CourseHoles
                .Select(h => new CourseHole
                {
                    ID = 0,
                    CourseID = 0,
                    HoleNumber = h.HoleNumber,
                    Par = h.Par,
                    Handicap = h.Handicap,
                    Yardage = h.Yardage
                })
                .ToList()
        };

        await _courseRepository.SaveItemAsync(copied);

        // After saving the copy, navigate directly to the edit screen for the new course
        SelectedCourse = null;
        await Shell.Current.GoToAsync($"course-edit?courseId={copied.ID}");
    }

    [RelayCommand]
    private async Task DeleteCourseAsync(CourseWithLastPlayed? courseWithLastPlayed)
    {
        if (courseWithLastPlayed is null)
            return;

        var course = courseWithLastPlayed.Course;

        var confirm = await Shell.Current.DisplayAlert(
            AppResources.DeleteCourseTitle,
            string.Format(AppResources.DeleteCourseMessage, course.Name),
            AppResources.Delete,
            AppResources.Cancel);

        if (!confirm)
            return;

        await _courseRepository.DeleteItemAsync(course);
        await NavigatedToAsync();
    }

    // Apply the current sort to the Courses collection
    private void ApplySort()
    {
        if (!_isSortAscending.HasValue)
            return;

        List<CourseWithLastPlayed> sorted;
        if (_isSortAscending.Value)
        {
            sorted = Courses
                .OrderBy(c => c.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        else
        {
            sorted = Courses
                .OrderByDescending(c => c.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        Courses.Clear();
        foreach (var c in sorted)
            Courses.Add(c);
    }

    [RelayCommand]
    private void SortByNameAsc()
    {
        _isSortAscending = true;
        ApplySort();
    }

    [RelayCommand]
    private void SortByNameDesc()
    {
        _isSortAscending = false;
        ApplySort();
    }
}
