using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace iDoublePress.PageModels;

public partial class AnalysisPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    private readonly CourseRepository _courseRepository;

    public ISeries[] ScoreSeries { get; set; } = Array.Empty<ISeries>();
    public ISeries[] AverageScoreSeries { get; set; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; set; } = Array.Empty<Axis>();
    public Axis[] YAxes { get; set; } = Array.Empty<Axis>();
    public Axis[] CourseXAxes { get; set; } = Array.Empty<Axis>();

    public AnalysisPageModel(RoundRepository roundRepository, CourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
    }

    [RelayCommand]
    private async Task NavigatedToAsync()
    {
        var rounds = await _roundRepository.ListAsync();
        var courses = await _courseRepository.ListAsync();

        // Score trends over time
        var scoreData = rounds
            .Where(r => r.EndTime.HasValue)
            .OrderBy(r => r.StartTime)
            .Select(r => new { Date = r.StartTime, Score = r.ScoreRelativeToPar })
            .ToList();

        ScoreSeries = new ISeries[]
        {
            new LineSeries<int>
            {
                Values = scoreData.Select(d => d.Score).ToArray(),
                Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Labels = scoreData.Select(d => d.Date.ToString("MMM dd")).ToArray()
            }
        };

        YAxes = new Axis[]
        {
            new Axis
            {
                Name = "Score Relative to Par"
            }
        };

        // Average scores by course
        var courseAverages = rounds
            .Where(r => r.Course != null)
            .GroupBy(r => r.Course!.Name)
            .Select(g => new { CourseName = g.Key, AverageScore = g.Average(r => r.ScoreRelativeToPar) })
            .ToList();

        AverageScoreSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = courseAverages.Select(c => c.AverageScore).ToArray(),
                Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColors.Green.WithAlpha(100))
            }
        };

        CourseXAxes = new Axis[]
        {
            new Axis
            {
                Labels = courseAverages.Select(c => c.CourseName).ToArray(),
                LabelsRotation = 45
            }
        };

        OnPropertyChanged(nameof(ScoreSeries));
        OnPropertyChanged(nameof(AverageScoreSeries));
        OnPropertyChanged(nameof(XAxes));
        OnPropertyChanged(nameof(YAxes));
        OnPropertyChanged(nameof(CourseXAxes));
    }
}