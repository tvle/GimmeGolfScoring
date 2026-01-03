using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;

namespace iDoublePress.PageModels;

public partial class CoursesPageModel : ObservableObject
{
    private readonly CourseRepository _courseRepository;

    public ObservableCollection<Course> Courses { get; } = new();

    [ObservableProperty]
    private Course? selectedCourse;

    [ObservableProperty]
    private bool isBusy;

    public CoursesPageModel(CourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
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
            foreach (var c in courses)
                Courses.Add(c);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenCourseAsync(Course? course)
    {
        if (course is null)
            return;

        SelectedCourse = null;
        await Shell.Current.GoToAsync($"course-edit?courseId={course.ID}");
    }

    [RelayCommand]
    private async Task EditCourseAsync(Course? course)
    {
        await OpenCourseAsync(course);
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
    private async Task CopyCourseAsync(Course? course)
    {
        if (course is null)
            return;

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
        await NavigatedToAsync();
    }

    [RelayCommand]
    private async Task DeleteCourseAsync(Course? course)
    {
        if (course is null)
            return;

        var confirm = await Shell.Current.DisplayAlert(
            "Delete Course",
            $"Delete '{course.Name}'?",
            "Delete",
            "Cancel");

        if (!confirm)
            return;

        await _courseRepository.DeleteItemAsync(course);
        await NavigatedToAsync();
    }
}
