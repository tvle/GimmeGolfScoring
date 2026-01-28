using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace iDoublePress.PageModels;

public partial class AnalysisPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    private readonly CourseRepository _courseRepository;

    private readonly List<Round> _allRounds = new();
    private List<Round> _displayRounds = new();
    // Cache for segments to avoid re-fetching on every filter change if possible, 
    // but simplified here to fetch on filter for accuracy.
    private List<ShotSegment> _currentSegments = new();

    public ObservableCollection<string> Years { get; } = new();

    [ObservableProperty]
    private string selectedYear;

    [ObservableProperty]
    private bool isBusy; // Added for async loading UI

    public int RoundCount => _displayRounds?.Count ?? 0;

    public string Title => string.Format("{0} ({1})", GetLocalized("Analysis"), RoundCount);

    // --- 1. Strokes Gained (Existing) ---
    public ISeries[] StrokesGainedSeries { get; set; } = Array.Empty<ISeries>();
    public Axis[] SGAxesX { get; set; } = Array.Empty<Axis>();
    public Axis[] SGAxesY { get; set; } = Array.Empty<Axis>();

    // --- 2. Driving Stats (NEW) ---
    [ObservableProperty]
    private string averageDriveDisplay = "---";
    [ObservableProperty]
    private string longestDriveDisplay = "---";

    // Combined with existing Bias
    public ISeries[] DrivingBiasSeries { get; set; } = Array.Empty<ISeries>();

    // --- 3. Approach Accuracy (NEW) ---
    public ISeries[] ApproachAccuracySeries { get; set; } = Array.Empty<ISeries>();
    public Axis[] ApproachXAxes { get; set; } = Array.Empty<Axis>();

    // --- 4. Club Distances (NEW) ---
    public ISeries[] ClubDistanceSeries { get; set; } = Array.Empty<ISeries>();
    public Axis[] ClubYAxes { get; set; } = Array.Empty<Axis>(); // Clubs on Y for RowSeries
    public Axis[] ClubXAxes { get; set; } = Array.Empty<Axis>();

    // --- 5. Putting (Existing) ---
    public ISeries[] PuttingStatSeries { get; set; } = Array.Empty<ISeries>();
    public Axis[] PuttingXAxes { get; set; } = Array.Empty<Axis>();

    // --- 6. Scrambling (Existing) ---
    public ISeries[] ScramblingSeries { get; set; } = Array.Empty<ISeries>();

    // --- 7. History (Existing) ---
    public ISeries[] ScoreSeries { get; set; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; set; } = Array.Empty<Axis>();
    public Axis[] YAxes { get; set; } = Array.Empty<Axis>();

    public AnalysisPageModel(RoundRepository roundRepository, CourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;

        SelectedYear = DateTime.Now.Year.ToString();
        Years.Add(AppResources.All);
        Years.Add(DateTime.Now.Year.ToString());
    }

    private string GetLocalized(string key) => Resources.Strings.AppResources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    [RelayCommand]
    private async Task NavigatedToAsync()
    {
        IsBusy = true;
        try
        {
            var rounds = await _roundRepository.ListAsync();
            _allRounds.Clear();
            foreach (var r in rounds) _allRounds.Add(r);

            PopulateYears();

            var currentYearString = DateTime.Now.Year.ToString();
            if (Years.Contains(currentYearString)) SelectedYear = currentYearString;
            else if (Years.Contains(AppResources.All)) SelectedYear = AppResources.All;

            await ApplyFilterAndCalculateAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Changed to Async to handle DB fetching
    async partial void OnSelectedYearChanged(string value)
    {
        await ApplyFilterAndCalculateAsync();
    }

    private async Task ApplyFilterAndCalculateAsync()
    {
        IsBusy = true;
        try
        {
            IEnumerable<Round> filtered = SelectedYear == AppResources.All
                ? _allRounds
                : _allRounds.Where(r => r.StartTime.Year.ToString() == SelectedYear);

            _displayRounds = filtered.OrderBy(r => r.StartTime).ToList();
            var allHoles = _displayRounds.SelectMany(r => r.Holes).ToList();

            // Clear old data
            if (!allHoles.Any())
            {
                ClearCharts();
                return;
            }

            // 1. Fetch ALL segments for these rounds (Performance intensive, but necessary for aggregates)
            _currentSegments.Clear();
            foreach (var hole in allHoles)
            {
                var segs = await _roundRepository.GetShotSegmentsForHoleAsync(hole.ID);
                _currentSegments.AddRange(segs);
            }

            // 2. Calculate All Stats
            CalculateStrokesGained(allHoles);
            CalculateDrivingStats(allHoles); // Updates Bias Chart + New Text Stats
            CalculateApproachStats();        // New Chart
            CalculateClubStats();            // New Chart
            CalculatePuttingStats(allHoles);
            CalculateScrambling(allHoles);
            CalculateScoreTrends(_displayRounds);

            NotifyAllCharts();
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(RoundCount));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Analysis Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearCharts()
    {
        StrokesGainedSeries = Array.Empty<ISeries>();
        DrivingBiasSeries = Array.Empty<ISeries>();
        ApproachAccuracySeries = Array.Empty<ISeries>();
        ClubDistanceSeries = Array.Empty<ISeries>();
        PuttingStatSeries = Array.Empty<ISeries>();
        ScramblingSeries = Array.Empty<ISeries>();
        ScoreSeries = Array.Empty<ISeries>();

        AverageDriveDisplay = "---";
        LongestDriveDisplay = "---";

        NotifyAllCharts();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(RoundCount));
    }

    // ... [Keep CalculateStrokesGained, CalculatePuttingStats, CalculateScrambling, CalculateScoreTrends as they were] ...
    // For brevity, I am not repeating the unchanged methods here, but YOU SHOULD KEEP THEM.
    // I will insert the NEW calculation methods below.

    private void CalculateDrivingStats(List<Hole> holes)
    {
        // A. Existing Bias Chart Logic
        var drivingHoles = holes.Where(h => h.Par > 3).ToList();
        var lefts = drivingHoles.Count(h => h.FairwayResult == FairwayResult.Left);
        var rights = drivingHoles.Count(h => h.FairwayResult == FairwayResult.Right);
        var centers = drivingHoles.Count(h => h.FairwayResult == FairwayResult.Fairway);

        DrivingBiasSeries = new ISeries[]
        {
            new PieSeries<int> { Values = new[] { lefts }, Name = GetLocalized("Left"), Fill = new SolidColorPaint(SKColors.OrangeRed) },
            new PieSeries<int> { Values = new[] { centers }, Name = GetLocalized("Fairway"), Fill = new SolidColorPaint(SKColors.ForestGreen) },
            new PieSeries<int> { Values = new[] { rights }, Name = GetLocalized("Right"), Fill = new SolidColorPaint(SKColors.Orange) }
        };

        // B. New Distance Logic
        if (_currentSegments.Any())
        {
            var drives = _currentSegments.Where(s => s.Tag == AppResources.Tag_D || s.Tag == "Driver");
            var distances = new List<double>();

            foreach (var d in drives)
            {
                string cleanDist = d.DistanceDisplay.Replace("y", "").Replace("m", "").Trim();
                if (double.TryParse(cleanDist, out double val) && val > 0)
                    distances.Add(val);
            }

            if (distances.Any())
            {
                string unit = RegionInfo.CurrentRegion.IsMetric ? "m" : "y";
                AverageDriveDisplay = $"{distances.Average():F0}{unit}";
                LongestDriveDisplay = $"{distances.Max():F0}{unit}";
            }
            else
            {
                AverageDriveDisplay = "---";
                LongestDriveDisplay = "---";
            }
        }
    }

    private void CalculateApproachStats()
    {
        // Count instances of "Green - Front", "Center", "Back"
        int front = _currentSegments.Count(s => s.Tag == AppResources.Tag_Front);
        int center = _currentSegments.Count(s => s.Tag == AppResources.Tag_Center);
        int back = _currentSegments.Count(s => s.Tag == AppResources.Tag_Back);

        if (front + center + back == 0)
        {
            ApproachAccuracySeries = Array.Empty<ISeries>();
            return;
        }

        ApproachAccuracySeries = new ISeries[]
        {
            new ColumnSeries<int> { Values = new[] { front }, Name = GetLocalized("Front"), Fill = new SolidColorPaint(SKColors.Goldenrod) },
            new ColumnSeries<int> { Values = new[] { center }, Name = GetLocalized("Center"), Fill = new SolidColorPaint(SKColors.ForestGreen) },
            new ColumnSeries<int> { Values = new[] { back }, Name = GetLocalized("Back"), Fill = new SolidColorPaint(SKColors.CornflowerBlue) }
        };

        ApproachXAxes = new Axis[]
        {
            new Axis { Labels = new[] { GetLocalized("Front"), GetLocalized("Center"), GetLocalized("Back") } }
        };
    }

    private void CalculateClubStats()
    {
        var clubStats = new List<ClubStat>();

        // Group by Tag
        var groups = _currentSegments.GroupBy(s => s.Tag);

        foreach (var grp in groups)
        {
            // Filter out non-clubs (Locations, empty tags)
            if (string.IsNullOrEmpty(grp.Key) ||
                grp.Key == AppResources.Tag_TeeBox ||
                grp.Key.StartsWith("Green") ||
                grp.Key == AppResources.Tag_Front ||
                grp.Key == AppResources.Tag_Center ||
                grp.Key == AppResources.Tag_Back)
                continue;

            var dists = new List<double>();
            foreach (var item in grp)
            {
                string clean = item.DistanceDisplay.Replace("y", "").Replace("m", "").Trim();
                if (double.TryParse(clean, out double v) && v > 0) dists.Add(v);
            }

            if (dists.Any())
            {
                clubStats.Add(new ClubStat
                {
                    Name = grp.Key,
                    Average = dists.Average()
                });
            }
        }

        if (!clubStats.Any())
        {
            ClubDistanceSeries = Array.Empty<ISeries>();
            return;
        }

        // Sort by distance (Wedges at top, Driver at bottom? Or reverse? Let's do longest at top)
        var sorted = clubStats.OrderBy(c => c.Average).ToList();

        // Use RowSeries (Horizontal Bars) for readable club names
        ClubDistanceSeries = new ISeries[]
        {
            new RowSeries<double>
            {
                Values = sorted.Select(c => c.Average).ToArray(),
                Fill = new SolidColorPaint(SKColors.SlateBlue),
                DataLabelsSize = 12,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                DataLabelsFormatter = (p) => $"{p.Model:F0}"
            }
        };

        ClubYAxes = new Axis[]
        {
            new Axis { Labels = sorted.Select(c => c.Name).ToArray() }
        };

        ClubXAxes = new Axis[]
        {
            new Axis { Labeler = val => $"{val:F0}" }
        };
    }

    // Helper class for calculation
    private class ClubStat { public string Name { get; set; } public double Average { get; set; } }

    private void PopulateYears()
    {
        var years = _allRounds.Select(r => r.StartTime.Year).Distinct().ToList();
        years.Add(DateTime.Now.Year);
        var yearStrings = years.Distinct().OrderByDescending(y => y).Select(y => y.ToString()).ToList();
        Years.Clear();
        Years.Add(AppResources.All);
        foreach (var y in yearStrings) Years.Add(y);
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
                Name = GetLocalized("StrokesGainedEst"),
                // Color bars Red (negative) or Green (positive) dynamically
                Fill = new SolidColorPaint(SKColors.SlateBlue)
            }
        };

        SGAxesX = new Axis[] { new Axis { Labels = new[] { GetLocalized("Driving"), GetLocalized("Approach"), GetLocalized("ShortGame"), GetLocalized("Putting") } } };
        SGAxesY = new Axis[] { new Axis { Labeler = value => value.ToString("N1") } }; // Show 1 decimal
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
            new ColumnSeries<double> { Values = new[] { shortPct, medPct, longPct }, Name = GetLocalized("YourPercentage") }
        };

        PuttingXAxes = new Axis[] { new Axis { Labels = new[] { GetLocalized("MakeUnder6ft"), GetLocalized("Make6to20ft"), GetLocalized("TwoPuttOver20ft") } } };
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
                Name = GetLocalized("ScramblingPercentage"),
                InnerRadius = 50, // Makes it a Donut Chart
                Fill = new SolidColorPaint(SKColors.Gold)
            },
            new PieSeries<double>
            {
                Values = new[] { failRate },
                Name = GetLocalized("Missed"),
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
        YAxes = new Axis[] { new Axis { Name = GetLocalized("ScoreToPar") } };
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
        OnPropertyChanged(nameof(AverageDriveDisplay));
        OnPropertyChanged(nameof(LongestDriveDisplay));
        OnPropertyChanged(nameof(ApproachAccuracySeries));
        OnPropertyChanged(nameof(ApproachXAxes));
        OnPropertyChanged(nameof(ClubDistanceSeries));
        OnPropertyChanged(nameof(ClubXAxes));
        OnPropertyChanged(nameof(ClubYAxes));

    }
}