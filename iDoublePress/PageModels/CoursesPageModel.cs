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
    private async Task AddCourseAsync()
    {
        await Shell.Current.GoToAsync("course-edit");
    }
}
