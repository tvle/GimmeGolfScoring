using iDoublePress.Models;
using Microsoft.Extensions.Logging;
using iDoublePress.Resources.Strings;

namespace iDoublePress.Data;

/// <summary>
/// Service for seeding initial golf course data.
/// </summary>
public class GolfSeedDataService
{
    private readonly CourseRepository _courseRepository;
    private readonly PlayerRepository _playerRepository;
    private readonly ILogger _logger;

    public GolfSeedDataService(CourseRepository courseRepository, PlayerRepository playerRepository, ILogger<GolfSeedDataService> logger)
    {
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _logger = logger;
    }

    public async Task LoadSeedDataAsync()
    {
        try
        {
            await SeedDefaultPlayerAsync();
            await SeedDefaultCoursesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error seeding golf data");
            throw;
        }
    }

    private async Task SeedDefaultPlayerAsync()
    {
        var players = await _playerRepository.ListAsync();
        if (players.Any())
            return; // Already seeded

        var defaultPlayer = new Player
        {
            Name = AppResources.Me,
            Handicap = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        await _playerRepository.SaveItemAsync(defaultPlayer);
        _logger.LogInformation(AppResources.DefaultPlayerCreated);
    }

    private async Task SeedDefaultCoursesAsync()
    {
        var courses = await _courseRepository.ListAsync();
        if (courses.Any())
            return; // Already seeded

        // Create standard par 72 course
        var par72Course = new Course
        {
            Name = AppResources.StandardCourse,
            Location = AppResources.DefaultLocation,
            TotalPar = 72,
            Holes = 18,
            Rating = 72.0m,
            Slope = 113,
            IsCustom = false,
            CreatedAt = DateTime.Now
        };

        // Standard par 72: 4 par 3s, 10 par 4s, 4 par 5s
        var par72Holes = new List<CourseHole>();
        var parPattern = new[] { 4, 5, 4, 3, 4, 4, 3, 4, 5, 4, 4, 3, 4, 5, 4, 4, 3, 5 };
        
        for (int i = 1; i <= 18; i++)
        {
            par72Holes.Add(new CourseHole
            {
                HoleNumber = i,
                Par = parPattern[i - 1],
                Handicap = i,
                Yardage = parPattern[i - 1] switch
                {
                    3 => 175,
                    4 => 400,
                    5 => 550,
                    _ => 400
                }
            });
        }
        
        par72Course.CourseHoles = par72Holes;
        await _courseRepository.SaveItemAsync(par72Course);
        _logger.LogInformation(AppResources.Par72CourseCreated);

        // Create 9-hole par 36 course
        var par36Course = new Course
        {
            Name = AppResources.NineHoleCourse,
            Location = AppResources.DefaultLocation,
            TotalPar = 36,
            Holes = 9,
            Rating = 36.0m,
            Slope = 113,
            IsCustom = false,
            CreatedAt = DateTime.Now
        };

        var par36Holes = new List<CourseHole>();
        var par36Pattern = new[] { 4, 5, 4, 3, 4, 4, 3, 4, 5 };
        
        for (int i = 1; i <= 9; i++)
        {
            par36Holes.Add(new CourseHole
            {
                HoleNumber = i,
                Par = par36Pattern[i - 1],
                Handicap = i,
                Yardage = par36Pattern[i - 1] switch
                {
                    3 => 175,
                    4 => 400,
                    5 => 550,
                    _ => 400
                }
            });
        }
        
        par36Course.CourseHoles = par36Holes;
        await _courseRepository.SaveItemAsync(par36Course);
        _logger.LogInformation(AppResources.Par36CourseCreated);
    }
}
