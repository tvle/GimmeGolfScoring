using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;

namespace iDoublePress.PageModels;

[QueryProperty(nameof(CourseId), "courseId")]
public partial class CourseEditPageModel : ObservableObject
{
    private readonly CourseRepository _courseRepository;

    [ObservableProperty]
    private int courseId;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private int numberOfHoles = 18;

    [ObservableProperty]
    private bool isNewCourse;

    public ObservableCollection<CourseHole> Holes { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    public CourseEditPageModel(CourseRepository courseRepository)
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
            
            if (CourseId > 0)
            {
                // Editing existing course
                IsNewCourse = false;
                var course = await _courseRepository.GetAsync(CourseId);
                if (course is null)
                    return;

                Name = course.Name;
                NumberOfHoles = course.Holes;

                Holes.Clear();
                foreach (var h in course.CourseHoles.OrderBy(h => h.HoleNumber))
                    Holes.Add(h);
            }
            else
            {
                // Creating new course
                IsNewCourse = true;
                Name = string.Empty;
                NumberOfHoles = 18;
                InitializeHoles();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetHoleCount(object? parameter)
    {
        if (parameter is null || !IsNewCourse)
            return;

        if (!int.TryParse(parameter.ToString(), out int holeCount))
            return;

        if (holeCount == NumberOfHoles)
            return;

        NumberOfHoles = holeCount;
        InitializeHoles();
    }

    private void InitializeHoles()
    {
        Holes.Clear();
        for (int i = 1; i <= NumberOfHoles; i++)
        {
            Holes.Add(new CourseHole
            {
                HoleNumber = i,
                Par = 4
            });
        }
    }

    [RelayCommand]
    private void IncrementPar(CourseHole? hole)
    {
        if (hole is null)
            return;

        hole.Par += 1;
    }

    [RelayCommand]
    private void DecrementPar(CourseHole? hole)
    {
        if (hole is null)
            return;

        if (hole.Par > 1)
            hole.Par -= 1;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            
            Course course;
            if (CourseId > 0)
            {
                // Update existing course
                course = await _courseRepository.GetAsync(CourseId);
                if (course is null)
                    return;
            }
            else
            {
                // Create new course
                course = new Course
                {
                    IsCustom = true,
                    CreatedAt = DateTime.UtcNow
                };
            }

            course.Name = Name;
            course.Holes = NumberOfHoles;
            course.CourseHoles = Holes.OrderBy(h => h.HoleNumber).ToList();
            course.TotalPar = course.CourseHoles.Sum(h => h.Par);

            await _courseRepository.SaveItemAsync(course);
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
