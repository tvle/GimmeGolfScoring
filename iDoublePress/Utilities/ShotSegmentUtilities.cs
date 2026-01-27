using iDoublePress.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace iDoublePress.Utilities;

/// <summary>
/// Task Utilities.
/// </summary>
public static class ShotSegmentUtilities
{
    public static void RecalculateDistances(List<ShotSegment> segments)
    {
        // Iterate from the NEWEST (Index 0) to the OLDEST (Index Count-1)
        for (int i = 0; i < segments.Count; i++)
        {
            // The last item in the list is the "Start Point" (no previous point to measure from)
            if (i == segments.Count - 1)
            {
                segments[i].DistanceDisplay = "---";
                continue;
            }

            // Calculate distance from the point "Below" this one (i + 1)
            // Example: Point A (0) measures distance from Point B (1)
            var currentPoint = segments[i].Point;
            var startPoint = segments[i + 1].Point;

            if (RegionInfo.CurrentRegion.IsMetric)
            {
                double kms = Location.CalculateDistance(startPoint, currentPoint, DistanceUnits.Kilometers);
                segments[i].DistanceDisplay = $"{kms * 1000:F0}m";
            }
            else
            {
                double miles = Location.CalculateDistance(startPoint, currentPoint, DistanceUnits.Miles);
                //for testing, generate a random distance between 100 and 250yards
                //segments[i].DistanceDisplay = $"{new Random().Next(100, 250)}y";
                segments[i].DistanceDisplay = $"{miles * 1760:F0}y";
            }
        }
    }
    public static List<ShotSegment> RecalculateDistances(ObservableCollection<ShotSegment> segments)
    {
        var segmentList = new List<ShotSegment>(segments);
        RecalculateDistances(segmentList);
        return segmentList;
    }
}