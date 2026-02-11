using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;
using System.Collections.ObjectModel;
using iDoublePress.Utilities;

namespace iDoublePress.PageModels;

[QueryProperty(nameof(RoundId), "roundId")]
[QueryProperty(nameof(CurrentHoleIndex), "holeIndex")]
public partial class ShowGPSPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    private readonly ModalErrorHandler _errorHandler;

    // --- GPS accuracy configuration ---
    /// <summary>Maximum acceptable accuracy in meters. Readings worse than this are rejected.</summary>
    private const double AccuracyThresholdMeters = 12.0;
    /// <summary>Number of good GPS samples to collect and average for each measurement.</summary>
    private const int MinGoodSamples = 3;
    /// <summary>Maximum individual sample attempts before giving up.</summary>
    private const int MaxSampleAttempts = 8;
    /// <summary>Delay between GPS sample attempts in milliseconds.</summary>
    private const int SampleDelayMs = 600;

    // --- Location listener state ---
    private bool _isListening;
    private Location? _latestGoodLocation;
    private readonly object _locationLock = new();

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

    /// <summary>
    /// Current GPS signal quality indicator shown in the UI.
    /// Values: "🔴 No Signal", "🟡 Acquiring...", "🟢 Ready (±Xm)"
    /// </summary>
    [ObservableProperty]
    private string gpsStatusDisplay = "🔴 No Signal";

    /// <summary>
    /// Color for the GPS status text.
    /// </summary>
    [ObservableProperty]
    private Color gpsStatusColor = Colors.Red;

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

    /// <summary>
    /// Starts a foreground location listener to keep the GPS chip warm and provide
    /// continuous high-accuracy fixes. This eliminates cold-start delays that cause
    /// the first reading to be wildly inaccurate.
    /// </summary>
    public async Task StartLocationListenerAsync()
    {
        if (_isListening) return;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    GpsStatusDisplay = AppResources.GpsPermissionDenied;
                    GpsStatusColor = Colors.Red;
                    return;
                }
            }

            Geolocation.Default.LocationChanged += OnLocationChanged;

            var listeningRequest = new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(1));
            var started = await Geolocation.Default.StartListeningForegroundAsync(listeningRequest);

            if (started)
            {
                _isListening = true;
                GpsStatusDisplay = AppResources.GpsAcquiring;
                GpsStatusColor = Colors.Orange;
            }
            else
            {
                GpsStatusDisplay = AppResources.GpsListenerFailed;
                GpsStatusColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start location listener: {ex.Message}");
            GpsStatusDisplay = AppResources.GpsError;
            GpsStatusColor = Colors.Red;
        }
    }

    /// <summary>
    /// Stops the foreground location listener to conserve battery.
    /// Called when navigating away from the GPS page.
    /// </summary>
    public void StopLocationListener()
    {
        if (!_isListening) return;

        try
        {
            Geolocation.Default.LocationChanged -= OnLocationChanged;
            Geolocation.Default.StopListeningForeground();
            _isListening = false;
            _latestGoodLocation = null;
            GpsStatusDisplay = AppResources.GpsStopped;
            GpsStatusColor = Colors.Gray;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to stop location listener: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles continuous location updates from the foreground listener.
    /// Updates the GPS status indicator and caches the latest good fix.
    /// </summary>
    private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        var loc = e.Location;
        if (loc == null) return;

        var accuracy = loc.Accuracy ?? double.MaxValue;

        lock (_locationLock)
        {
            if (accuracy <= AccuracyThresholdMeters)
            {
                _latestGoodLocation = loc;
            }
        }

        // Update UI on main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (accuracy <= 5.0)
            {
                GpsStatusDisplay = string.Format(AppResources.GpsReadyFormat, accuracy);
                GpsStatusColor = Colors.Green;
            }
            else if (accuracy <= AccuracyThresholdMeters)
            {
                GpsStatusDisplay = string.Format(AppResources.GpsOkFormat, accuracy);
                GpsStatusColor = Colors.Orange;
            }
            else
            {
                GpsStatusDisplay = string.Format(AppResources.GpsWeakFormat, accuracy);
                GpsStatusColor = Colors.Red;
            }
        });
    }

    /// <summary>
    /// Acquires a high-accuracy GPS location by collecting multiple samples,
    /// filtering by accuracy threshold, and averaging the results.
    /// Falls back to the cached listener location if available.
    /// </summary>
    private async Task<Location?> GetHighAccuracyLocationAsync()
    {
        var goodReadings = new List<Location>();

        for (int attempt = 0; attempt < MaxSampleAttempts; attempt++)
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
                var loc = await Geolocation.Default.GetLocationAsync(request);

                if (loc != null)
                {
                    var accuracy = loc.Accuracy ?? double.MaxValue;
                    Console.WriteLine($"GPS sample {attempt + 1}: accuracy={accuracy:F1}m, lat={loc.Latitude:F7}, lng={loc.Longitude:F7}");

                    if (accuracy <= AccuracyThresholdMeters)
                    {
                        goodReadings.Add(loc);
                        if (goodReadings.Count >= MinGoodSamples)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GPS sample {attempt + 1} failed: {ex.Message}");
            }

            if (attempt < MaxSampleAttempts - 1)
                await Task.Delay(SampleDelayMs);
        }

        // If we got enough good readings, average them
        if (goodReadings.Count >= 2)
        {
            var avgLat = goodReadings.Average(l => l.Latitude);
            var avgLng = goodReadings.Average(l => l.Longitude);
            var avgAccuracy = goodReadings.Average(l => l.Accuracy ?? 0);

            return new Location(avgLat, avgLng) { Accuracy = avgAccuracy };
        }

        // If we got at least one good reading, use it
        if (goodReadings.Count == 1)
            return goodReadings[0];

        // Last resort: use cached listener location if available
        lock (_locationLock)
        {
            if (_latestGoodLocation != null)
            {
                Console.WriteLine("GPS: Using cached listener location as fallback.");
                return _latestGoodLocation;
            }
        }

        return null;
    }

    [RelayCommand]
    private async Task ToggleMeasurement()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            // 1. Check permissions
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted) return;
            }

            // 2. Get high-accuracy averaged location
            var location = await GetHighAccuracyLocationAsync();

            if (location == null)
            {
                await Shell.Current.DisplayAlertAsync(
                    AppResources.GpsLowAccuracyTitle,
                    string.Format(AppResources.GpsLowAccuracyMessage, AccuracyThresholdMeters),
                    AppResources.OK);
                return;
            }

            var accuracy = location.Accuracy ?? 0;

            // 3. Add New Segment to Top of List
            var newSegment = new ShotSegment
            {
                CreatedAt = DateTime.UtcNow,
                HoleID = CurrentHole.ID,
                Point = location,
                AccuracyMeters = accuracy > 0 ? accuracy : null,
                LocationDisplay = $"{location.Latitude:F7}, {location.Longitude:F7}",
                DistanceDisplay = "---" // Temporary, will be fixed by Recalculate
            };

            ShotSegments.Insert(0, newSegment);

            // 4. Recalculate all distances based on the new list order
            var segmentsUpdated = ShotSegmentUtilities.RecalculateDistances(ShotSegments);
            ShotSegments.Clear();
            foreach (var s in segmentsUpdated)
                ShotSegments.Add(s);

            // 5. Persist segments for this hole
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
            var segmentsUpdated = ShotSegmentUtilities.RecalculateDistances(ShotSegments);
            ShotSegments.Clear();
            foreach (var s in segmentsUpdated)
                ShotSegments.Add(s);

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


    async partial void OnRoundIdChanged(int value)
    {
        try
        {
            if (value > 0)
            {
                IsBusy = true;
                CurrentRound = await _roundRepository.GetAsync(roundId);
                await GetShotSegments();    // for some reason on Android, the OnCurrentHoleIndexChanged doesn't fire so calling this here also
                // Start the location listener to warm up the GPS chip
                await StartLocationListenerAsync();
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

    private async Task GetShotSegments()
    {
        CurrentHole = CurrentRound?.Holes[CurrentHoleIndex];
        // Load persisted shot segments for this hole
        ShotSegments.Clear();
        try
        {
            var loaded = await _roundRepository.GetShotSegmentsForHoleAsync(CurrentHole.ID);
            foreach (var s in loaded)
                ShotSegments.Add(s);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to load shot segments: {ex.Message}");
        }
        UpdateDisplay();
    }

    async partial void OnCurrentHoleIndexChanged(int value)
    {
        try
        {
            if (value > 0)
            {
                IsBusy = true;
                await GetShotSegments();                
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

    public async Task PersistShotSegmentsAsync()
    {
        try
        {
            if (CurrentHole != null)
            {
                await _roundRepository.SaveShotSegmentsForHoleAsync(CurrentHole.ID, ShotSegments);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error persisting shot segments: {ex.Message}");
        }
    }
    [RelayCommand]
    private async void Back()
    {
        StopLocationListener();
        await Shell.Current.GoToAsync("..");
    }
}