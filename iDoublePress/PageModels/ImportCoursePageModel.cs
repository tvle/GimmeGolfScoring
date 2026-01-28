using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;
using System.Text.Json;

namespace iDoublePress.PageModels;

public partial class ImportCoursePageModel : ObservableObject
{
    private readonly CourseRepository _courseRepository;

    [ObservableProperty]
    private string jsonInput = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public ImportCoursePageModel(CourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(JsonInput))
        {
            await Shell.Current.DisplayAlert("Error", "Please paste the course JSON data.", "OK");
            return;
        }

        try
        {
            IsBusy = true;

            // 1. Deserialization options to be lenient with case
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var course = JsonSerializer.Deserialize<Course>(JsonInput, options);

            if (course == null)
                throw new Exception("JSON could not be parsed into a Course object.");

            // 2. Validation
            if (string.IsNullOrWhiteSpace(course.Name))
                throw new Exception("Course Name is required.");

            if (course.CourseHoles == null || !course.CourseHoles.Any() || (course.CourseHoles.Count != course.Holes))
                throw new Exception($"Hole count mismatch. Defined: {course.Holes}, Found: {course.CourseHoles.Count}");

            // 3. Data Cleanup (Ensure we create a NEW course, don't overwrite existing IDs)
            course.ID = 0;
            course.CreatedAt = DateTime.Now;

            // Recalculate Total Par based on holes to ensure data integrity
            course.TotalPar = course.CourseHoles.Sum(h => h.Par);

            foreach (var hole in course.CourseHoles)
            {
                hole.ID = 0;
                hole.CourseID = 0;
            }

            // 4. Save to Database
            await _courseRepository.SaveItemAsync(course);

            await Shell.Current.DisplayAlert("Success", $"{course.Name} imported successfully!", "OK");

            // Navigate back to the list
            await Shell.Current.GoToAsync("..");
        }
        catch (JsonException jex)
        {
            await Shell.Current.DisplayAlert("Invalid JSON", $"Syntax error in JSON: {jex.Message}", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Import Failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}