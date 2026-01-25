using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace iDoublePress.PageModels;

public partial class ShowGPSPageModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy = false;

    public ObservableCollection<ShotSegment> ShotSegments { get; } = new();

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
}