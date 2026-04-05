using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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

    [ObservableProperty]
    private int totalPar;

    [ObservableProperty]
    private int? totalYardage;

    [ObservableProperty]
    private string? courseRating;

    [ObservableProperty]
    private string? slope;

    public ObservableCollection<CourseHole> Holes { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isDirty;

    private bool _suppressDirtyTracking;

    public CourseEditPageModel(CourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
        Holes.CollectionChanged += OnHolesCollectionChanged;
    }

    private void OnHolesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (CourseHole hole in e.OldItems)
            {
                hole.PropertyChanged -= OnHolePropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (CourseHole hole in e.NewItems)
            {
                hole.PropertyChanged += OnHolePropertyChanged;
            }
        }

        UpdateTotals();

        if (!_suppressDirtyTracking && (e.Action != NotifyCollectionChangedAction.Reset || e.NewItems != null || e.OldItems != null))
        {
            IsDirty = true;
        }
    }

    private void OnHolePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CourseHole.Par) || e.PropertyName == nameof(CourseHole.Yardage))
        {
            UpdateTotals();

            if (!_suppressDirtyTracking)
            {
                IsDirty = true;
            }
        }
    }

    private void UpdateTotals()
    {
        TotalPar = Holes.Sum(h => h.Par);

        var yards = Holes
            .Select(h => h.Yardage)
            .Where(y => y.HasValue)
            .Select(y => y!.Value)
            .ToList();

        TotalYardage = yards.Count == 0 ? null : yards.Sum();
    }

    partial void OnNameChanged(string value) => MarkDirty();

    partial void OnNumberOfHolesChanged(int value) => MarkDirty();

    partial void OnCourseRatingChanged(string? value) => MarkDirty();

    partial void OnSlopeChanged(string? value) => MarkDirty();

    private void MarkDirty()
    {
        if (!_suppressDirtyTracking && !IsBusy)
        {
            IsDirty = true;
        }
    }

    [RelayCommand]
    private async Task NavigatedToAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            _suppressDirtyTracking = true;
            
            if (CourseId > 0)
            {
                // Editing existing course
                IsNewCourse = false;
                
                var course = await _courseRepository.GetAsync(CourseId);
                if (course is null)
                    return;

                Name = course.Name;
                NumberOfHoles = course.Holes;
                CourseRating = course.Rating.HasValue ? course.Rating.Value.ToString("F1") : string.Empty;
                Slope = course.Slope.HasValue ? course.Slope.Value.ToString() : string.Empty;

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
                CourseRating = string.Empty;
                Slope = string.Empty;
                InitializeHoles();
            }

            UpdateTotals();
            IsDirty = false;
        }
        finally
        {
            _suppressDirtyTracking = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetHoleCount(object? parameter)
    {
        if (parameter is null)
            return;

        if (!int.TryParse(parameter.ToString(), out int holeCount))
            return;

        if (holeCount == NumberOfHoles)
            return;

        NumberOfHoles = holeCount;
        
        // For existing courses, add or remove holes as needed
        if (!IsNewCourse)
        {
            if (holeCount > Holes.Count)
            {
                // Add holes
                for (int i = Holes.Count + 1; i <= holeCount; i++)
                {
                    Holes.Add(new CourseHole
                    {
                        HoleNumber = i,
                        Par = 4
                    });
                }
            }
            else if (holeCount < Holes.Count)
            {
                // Remove holes from the end
                while (Holes.Count > holeCount)
                {
                    Holes.RemoveAt(Holes.Count - 1);
                }
            }
        }
        else
        {
            // For new courses, reinitialize all holes
            InitializeHoles();
        }

        UpdateTotals();
        MarkDirty();
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
        await SaveCourseAsync(navigateBack: true);
    }

    public async Task<bool> SaveCourseAsync(bool navigateBack)
    {
        if (IsBusy)
            return false;

        try
        {
            IsBusy = true;
            
            Course course;
            if (CourseId > 0)
            {
                // Update existing course
                course = await _courseRepository.GetAsync(CourseId);
                if (course is null)
                    return false;
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

            // Parse and validate Course Rating (CR)
            if (!string.IsNullOrWhiteSpace(CourseRating))
            {
                if (decimal.TryParse(CourseRating, out decimal cr))
                {
                    if (cr >= 60.0m && cr <= 80.0m)
                    {
                        course.Rating = cr;
                    }
                }
            }
            else
            {
                course.Rating = null;
            }

            // Parse and validate Slope
            if (!string.IsNullOrWhiteSpace(Slope))
            {
                if (int.TryParse(Slope, out int slopeValue))
                {
                    if (slopeValue >= 55 && slopeValue <= 155)
                    {
                        course.Slope = slopeValue;
                    }
                }
            }
            else
            {
                course.Slope = null;
            }

            await _courseRepository.SaveItemAsync(course);
            IsDirty = false;

            if (navigateBack)
            {
                await Shell.Current.GoToAsync("..");
            }

            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
