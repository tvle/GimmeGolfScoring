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
            var course = await _courseRepository.GetAsync(CourseId);
            if (course is null)
                return;

            Name = course.Name;

            Holes.Clear();
            foreach (var h in course.CourseHoles.OrderBy(h => h.HoleNumber))
                Holes.Add(h);
        }
        finally
        {
            IsBusy = false;
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
            var course = await _courseRepository.GetAsync(CourseId);
            if (course is null)
                return;

            course.Name = Name;
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
