using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;
using System.Collections.ObjectModel;
using System.Globalization;

namespace iDoublePress.PageModels;

[QueryProperty(nameof(RoundId), "roundId")]
[QueryProperty(nameof(CurrentHoleIndex), "holeIndex")]
public partial class ShowGPSPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    private readonly ModalErrorHandler _errorHandler;

    [ObservableProperty]
    private bool isBusy = false;

    [ObservableProperty]
    private int roundId;

    [ObservableProperty]
    private int currentHoleIndex;

    [ObservableProperty]
    private Round? currentRound;

    [ObservableProperty]
    private Hole? currentHole;

    public ObservableCollection<ShotSegment> ShotSegments { get; } = new();

    public string CurrentHoleNumberDisplay =>
        CurrentHole != null ? CurrentHole.HoleNumber.ToString() : "1";
    public string CurrentParNumberDisplay =>
        CurrentHole != null ? string.Format(AppResources.ParFormat, CurrentHole.Par) : string.Format(AppResources.ParFormat, 4);

    public ShowGPSPageModel(RoundRepository roundRepository, ModalErrorHandler errorHandler)
    {
        _roundRepository = roundRepository;
        _errorHandler = errorHandler;
    }

    [RelayCommand]
    private async Task ToggleMeasurement()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            // 1. Get Location
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted) return;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15));
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location == null) return;

            // 2. Add New Segment to Top of List
            var newSegment = new ShotSegment
            {
                Point = location,
                LocationDisplay = $"{location.Latitude:F7}, {location.Longitude:F7}",
                DistanceDisplay = "---" // Temporary, will be fixed by Recalculate
            };

            ShotSegments.Insert(0, newSegment);

            // 3. Recalculate all distances based on the new list order
            RecalculateDistances();

            // Persist segments for this hole
            try
            {
                if (CurrentHole != null)
                {
                    await _roundRepository.SaveShotSegmentsForHoleAsync(CurrentHole.ID, ShotSegments);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving shot segments: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GPS Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void DeleteSegment(ShotSegment segment)
    {
        if (ShotSegments.Contains(segment))
        {
            ShotSegments.Remove(segment);
            RecalculateDistances();

            // Persist removal
            try
            {
                if (CurrentHole != null)
                {
                    _ = _roundRepository.SaveShotSegmentsForHoleAsync(CurrentHole.ID, ShotSegments);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving shot segments after delete: {ex.Message}");
            }
        }
    }

    private void RecalculateDistances()
    {
        // Iterate from the NEWEST (Index 0) to the OLDEST (Index Count-1)
        for (int i = 0; i < ShotSegments.Count; i++)
        {
            // The last item in the list is the "Start Point" (no previous point to measure from)
            if (i == ShotSegments.Count - 1)
            {
                ShotSegments[i].DistanceDisplay = "---";
                continue;
            }

            // Calculate distance from the point "Below" this one (i + 1)
            // Example: Point A (0) measures distance from Point B (1)
            var currentPoint = ShotSegments[i].Point;
            var startPoint = ShotSegments[i + 1].Point;

            if (RegionInfo.CurrentRegion.IsMetric)
            {
                double kms = Location.CalculateDistance(startPoint, currentPoint, DistanceUnits.Kilometers);
                ShotSegments[i].DistanceDisplay = $"{kms * 1000:F0}m";
            }
            else
            {
                double miles = Location.CalculateDistance(startPoint, currentPoint, DistanceUnits.Miles);
                ShotSegments[i].DistanceDisplay = $"{miles * 1760:F0}y";
            }
        }
    }

    async partial void OnRoundIdChanged(int value)
    {
        try
        {
            if (value > 0)
            {
                IsBusy = true;
                CurrentRound = await _roundRepository.GetAsync(roundId);
            }
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
    async partial void OnCurrentHoleIndexChanged(int value)
    {
        try
        {
            if (value > 0)
            {
                IsBusy = true;
                CurrentHole = CurrentRound.Holes[CurrentHoleIndex];
                UpdateDisplay();

                // Load persisted shot segments for this hole
                ShotSegments.Clear();
                try
                {
                    var loaded = await _roundRepository.GetShotSegmentsForHoleAsync(CurrentHole.ID);
                    foreach (var s in loaded)
                        ShotSegments.Add(s);
                    RecalculateDistances();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to load shot segments: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
    private void UpdateDisplay()
    {
        OnPropertyChanged(nameof(CurrentHoleNumberDisplay));
        OnPropertyChanged(nameof(CurrentParNumberDisplay));
    }

}