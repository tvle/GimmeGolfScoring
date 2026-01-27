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
                CreatedAt = DateTime.UtcNow,
                HoleID = CurrentHole.ID,
                Point = location,
                LocationDisplay = $"{location.Latitude:F7}, {location.Longitude:F7}",
                DistanceDisplay = "---" // Temporary, will be fixed by Recalculate
            };

            ShotSegments.Insert(0,newSegment);

            // 3. Recalculate all distances based on the new list order
            var segmentsUpdated = ShotSegmentUtilities.RecalculateDistances(ShotSegments);
            ShotSegments.Clear();
            foreach (var s in segmentsUpdated)
                ShotSegments.Add(s);

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
        await Shell.Current.GoToAsync("..");
    }
}