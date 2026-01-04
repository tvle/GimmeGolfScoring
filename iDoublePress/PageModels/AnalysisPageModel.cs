using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements; // Needed for Labels
using SkiaSharp;
using System.Collections.ObjectModel;

namespace iDoublePress.PageModels;

public partial class AnalysisPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    private readonly CourseRepository _courseRepository;

    // 1. Strokes Gained Lite (Bar Chart)
    public ISeries[] StrokesGainedSeries { get; set; } = Array.Empty<ISeries>();
    public Axis[] SGAxesX { get; set; } = Array.Empty<Axis>();
    public Axis[] SGAxesY { get; set; } = Array.Empty<Axis>();

    // 2. Driving Bias (Pie Chart)
    public ISeries[] DrivingBiasSeries { get; set; } = Array.Empty<ISeries>();

    // 3. Putting Performance (Grouped Column)
    public ISeries[] PuttingStatSeries { get; set; } = Array.Empty<ISeries>();
    public Axis[] PuttingXAxes { get; set; } = Array.Empty<Axis>();

    // 4. Scrambling (Gauge/Pie)
    public ISeries[] ScramblingSeries { get; set; } = Array.Empty<ISeries>();

    // Existing Trend Charts
    public ISeries[] ScoreSeries { get; set; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; set; } = Array.Empty<Axis>();
    public Axis[] YAxes { get; set; } = Array.Empty<Axis>();

    public AnalysisPageModel(RoundRepository roundRepository, CourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
    }

    [RelayCommand]
    private async Task NavigatedToAsync()
    {
        var rounds = await _roundRepository.ListAsync();
        // Flatten rounds to get all holes for granular analysis
        // Note: Ensure your Round object has a List<HoleScore> property
        var allHoles = rounds.SelectMany(r => r.Holes).ToList();

        if (!allHoles.Any()) return;

        CalculateStrokesGained(allHoles);
        CalculateDrivingBias(allHoles);
        CalculatePuttingStats(allHoles);
        CalculateScrambling(allHoles);
        CalculateScoreTrends(rounds); // Your existing logic moved here

        NotifyAllCharts();
    }

    private void CalculateStrokesGained(List<Hole> holes)
    {
        // Simple Algorithm to approximate Strokes Gained without PGA database
        double sgOffTee = 0;
        double sgApproach = 0;
        double sgPutting = 0;
        double sgShortGame = 0;

        foreach (var hole in holes)
        {
            // 1. Off Tee Logic
            if (hole.Penalties > 0) sgOffTee -= 2.0;
            else if (hole.FairwayResult == FairwayResult.Fairway) sgOffTee += 0.2;
            else sgOffTee -= 0.1; // Rough penalty

            // 2. Approach Logic
            if (hole.GreenInRegulation == true) sgApproach += 0.4;
            else sgApproach -= 0.2;

            // 3. Putting Logic
            // Gain strokes if you 1-putt, lose if you 3-putt
            if (hole.Putts == 1) sgPutting += 0.5;
            else if (hole.Putts >= 3) sgPutting -= 0.8;

            // 4. Short Game (Scrambling)
            // If missed GIR but made Par or better -> Huge Gain
            if ((hole.GreenInRegulation != true) && hole.Score <= hole.Par) sgShortGame += 0.6;
        }

        StrokesGainedSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = new double[] { sgOffTee, sgApproach, sgShortGame, sgPutting },
                Name = "Strokes Gained (Est.)",
                // Color bars Red (negative) or Green (positive) dynamically
                Fill = new SolidColorPaint(SKColors.SlateBlue)
            }
        };

        SGAxesX = new Axis[] { new Axis { Labels = new[] { "Driving", "Approach", "Short Gm", "Putting" } } };
        SGAxesY = new Axis[] { new Axis { Labeler = value => value.ToString("N1") } }; // Show 1 decimal
    }

    private void CalculateDrivingBias(List<Hole> holes)
    {
        // Filter only holes that are NOT Par 3s (if your model has Par info)
        var drivingHoles = holes.Where(h => h.Par > 3).ToList();

        var lefts = drivingHoles.Count(h => h.FairwayResult == FairwayResult.Left);
        var rights = drivingHoles.Count(h => h.FairwayResult == FairwayResult.Right);
        var centers = drivingHoles.Count(h => h.FairwayResult == FairwayResult.Fairway);

        DrivingBiasSeries = new ISeries[]
        {
            new PieSeries<int> { Values = new[] { lefts }, Name = "Left", Fill = new SolidColorPaint(SKColors.OrangeRed) },
            new PieSeries<int> { Values = new[] { centers }, Name = "Fairway", Fill = new SolidColorPaint(SKColors.ForestGreen) },
            new PieSeries<int> { Values = new[] { rights }, Name = "Right", Fill = new SolidColorPaint(SKColors.Orange) }
        };
    }

    private void CalculatePuttingStats(List<Hole> holes)
    {
        // < 6ft: We want Make %
        var shortAtt = holes.Count(h => h.ProximityIndex == 0);
        var shortMade = holes.Count(h => h.ProximityIndex == 0 && h.Putts == 1);
        double shortPct = shortAtt > 0 ? (double)shortMade / shortAtt * 100 : 0;

        // 6-20ft: We want Make %
        var medAtt = holes.Count(h => h.ProximityIndex == 1);
        var medMade = holes.Count(h => h.ProximityIndex == 1 && h.Putts == 1);
        double medPct = medAtt > 0 ? (double)medMade / medAtt * 100 : 0;

        // > 20ft: We want 3-Putt AVOIDANCE % (Lower is better, so let's show 2-Putt or Better %)
        var longAtt = holes.Count(h => h.ProximityIndex == 2);
        var longSaved = holes.Count(h => h.ProximityIndex == 2 && h.Putts <= 2);
        double longPct = longAtt > 0 ? (double)longSaved / longAtt * 100 : 0;

        PuttingStatSeries = new ISeries[]
        {
            new ColumnSeries<double> { Values = new[] { shortPct, medPct, longPct }, Name = "Your %" }
        };

        PuttingXAxes = new Axis[] { new Axis { Labels = new[] { "< 6ft Make %", "6-20ft Make %", "> 20ft 2-Putt %" } } };
    }

    private void CalculateScrambling(List<Hole> holes)
    {
        var missedGreens = holes.Where(h => h.GreenInRegulation != true).ToList();
        var savedPar = missedGreens.Count(h => h.Score <= h.Par);

        double scrambleRate = missedGreens.Any() ? (double)savedPar / missedGreens.Count * 100 : 0;
        double failRate = 100 - scrambleRate;

        ScramblingSeries = new ISeries[]
        {
            new PieSeries<double>
            {
                Values = new[] { scrambleRate },
                Name = "Scrambling %",
                InnerRadius = 50, // Makes it a Donut Chart
                Fill = new SolidColorPaint(SKColors.Gold)
            },
            new PieSeries<double>
            {
                Values = new[] { failRate },
                Name = "Missed",
                InnerRadius = 50,
                Fill = new SolidColorPaint(SKColors.LightGray)
            }
        };
    }

    private void CalculateScoreTrends(List<Round> rounds)
    {
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
                Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 3 },
                Fill = null,
                GeometrySize = 10,
                GeometryStroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 3 }
            }
        };

        XAxes = new Axis[] { new Axis { Labels = scoreData.Select(d => d.Date.ToString("MMM dd")).ToArray() } };
        YAxes = new Axis[] { new Axis { Name = "Score to Par" } };
    }

    private void NotifyAllCharts()
    {
        OnPropertyChanged(nameof(StrokesGainedSeries));
        OnPropertyChanged(nameof(SGAxesX));
        OnPropertyChanged(nameof(SGAxesY));
        OnPropertyChanged(nameof(DrivingBiasSeries));
        OnPropertyChanged(nameof(PuttingStatSeries));
        OnPropertyChanged(nameof(PuttingXAxes));
        OnPropertyChanged(nameof(ScramblingSeries));
        OnPropertyChanged(nameof(ScoreSeries));
        OnPropertyChanged(nameof(XAxes));
        OnPropertyChanged(nameof(YAxes));
    }
}