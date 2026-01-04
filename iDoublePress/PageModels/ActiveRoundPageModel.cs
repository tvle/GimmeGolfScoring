using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;

namespace iDoublePress.PageModels;

[QueryProperty(nameof(RoundId), "roundId")]
[QueryProperty(nameof(InitialHoleIndex), "holeIndex")]
public partial class ActiveRoundPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    private readonly ModalErrorHandler _errorHandler;

    [ObservableProperty]
    private Round? currentRound;

    [ObservableProperty]
    private Hole? currentHole;

    [ObservableProperty]
    private int currentHoleIndex = 0;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private int roundId;

    // Shell query properties are received as strings and converted; nullable numeric types can fail conversion.
    // Use a non-nullable int and treat 0 as "not specified".
    [ObservableProperty]
    private int initialHoleIndex;

    public string TotalScoreDisplay =>
        CurrentRound?.ScoreDisplay ?? "E";

    public string CourseParDisplay =>
        CurrentRound?.Course?.TotalPar != null 
            ? string.Format(AppResources.CourseParFormat, CurrentRound.Course.TotalPar)
            : string.Empty;

    public int TotalPutts =>
        CurrentRound?.Holes.Where(h => h.IsScored).Sum(h => h.Putts ?? 0) ?? 0;

    public int TotalGIR =>
        CurrentRound?.Holes.Where(h => h.IsScored).Count(h => h.GreenInRegulation == true) ?? 0;

    public string HolesCompleted =>
        CurrentRound != null ? $"{CurrentHoleIndex + 1}/{CurrentRound.Holes.Count}" : "0/18";

    public string CurrentHoleDisplay =>
        CurrentHole != null
            ? string.Format(AppResources.HoleFormat, CurrentHole.HoleNumber)
            : string.Format(AppResources.HoleFormat, 1);

    public string CurrentParDisplay =>
        CurrentHole != null
            ? string.Format(AppResources.ParFormat, CurrentHole.Par)
            : string.Format(AppResources.ParFormat, 4);

    public bool CanDecreaseScore => CurrentHole != null && CurrentHole.Score > 1;
    public bool CanGoBack => CurrentHoleIndex > 0;
    public bool CanGoForward => CurrentRound != null && CurrentHoleIndex < CurrentRound.Holes.Count;
    public bool IsLastHole => CurrentRound != null && CurrentHoleIndex == CurrentRound.Holes.Count - 1;

    public bool CanDecreasePutts => CurrentHole != null && CurrentHole.Putts.HasValue;

    public bool CanIncreasePutts
    {
        get
        {
            if (CurrentHole == null) return false;
            var current = CurrentHole.Putts ?? -1; // null => treat as -1 so first + sets to 0
            return current + 1 <= CurrentHole.Score;
        }
    }

    [RelayCommand]
    private void IncreasePutts()
    {
        if (CurrentHole == null) return;

        var next = (CurrentHole.Putts ?? -1) + 1; // null => first + makes it 0
        if (next < 0) next = 0;
        if (next > CurrentHole.Score) return;

        CurrentHole.Putts = next;
        CurrentHole.IsScored = true;
        UpdatePuttsDisplay();
    }

    [RelayCommand]
    private void DecreasePutts()
    {
        if (CurrentHole == null) return;
        if (!CurrentHole.Putts.HasValue) return;

        if (CurrentHole.Putts.Value == 0)
        {
            CurrentHole.Putts = null;
        }
        else
        {
            CurrentHole.Putts = CurrentHole.Putts.Value - 1;
        }

        CurrentHole.IsScored = true;
        UpdatePuttsDisplay();
    }

    public ActiveRoundPageModel(RoundRepository roundRepository, ModalErrorHandler errorHandler)
    {
        _roundRepository = roundRepository;
        _errorHandler = errorHandler;
    }

    async partial void OnRoundIdChanged(int value)
    {
        if (value > 0)
        {
            await LoadRound(value);
        }
    }

    private Hole? _trackedHole;

    private static void EnsureStatsDefaults(Hole? hole)
    {
        if (hole == null) return;

        // Only apply defaults for nullable stats; don't reset persisted per-hole values.
        hole.GreenInRegulation ??= false;

        // Penalty only makes sense for a miss.
        if (hole.FairwayResult is not (FairwayResult.Left or FairwayResult.Right))
        {
            hole.FairwayMissPenalty = false;
        }
    }

    async partial void OnCurrentHoleChanged(Hole? value)
    {
        if (_trackedHole != null)
        {
            _trackedHole.PropertyChanged -= CurrentHole_PropertyChanged;
        }

        _trackedHole = value;
        if (_trackedHole != null)
        {
            _trackedHole.PropertyChanged += CurrentHole_PropertyChanged;
        }

        EnsureStatsDefaults(value);
        UpdateDisplay();
        OnPropertyChanged(nameof(CanDecreasePutts));
        OnPropertyChanged(nameof(CanIncreasePutts));
        OnPropertyChanged(nameof(IsProximityS));
        OnPropertyChanged(nameof(IsProximityM));
        OnPropertyChanged(nameof(IsProximityL));
    }

    private async Task LoadRound(int roundId)
    {
        try
        {
            IsBusy = true;
            CurrentRound = await _roundRepository.GetAsync(roundId);

            if (CurrentRound != null && CurrentRound.Holes.Any())
            {
                if (InitialHoleIndex > 0)
                {
                    CurrentHoleIndex = Math.Clamp(InitialHoleIndex, 0, CurrentRound.Holes.Count - 1);
                }
                else
                {
                    var firstUnscoredIndex = CurrentRound.Holes.FindIndex(h => !h.IsScored);
                    CurrentHoleIndex = firstUnscoredIndex >= 0 ? firstUnscoredIndex : 0;
                }

                CurrentHole = CurrentRound.Holes[CurrentHoleIndex];
                EnsureStatsDefaults(CurrentHole);
                UpdateDisplay();
            }
            else
            {
                await Shell.Current.DisplayAlert(AppResources.Error, AppResources.RoundNotFound, AppResources.OK);
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception e)
        {
            _errorHandler.HandleError(e);
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void IncreaseScore()
    {
        if (CurrentHole != null)
        {
            CurrentHole.Score++;
            CurrentHole.IsScored = true;
            UpdateScoreDisplay();
        }
    }

    [RelayCommand]
    private void DecreaseScore()
    {
        if (CurrentHole != null && CurrentHole.Score > 0)
        {
            CurrentHole.Score--;
            CurrentHole.IsScored = true;
            UpdateScoreDisplay();
        }
    }

    [RelayCommand]
    private async Task NextHole()
    {
        if (CurrentRound == null) return;

        if (CurrentHole != null && !CurrentHole.IsScored)
        {
            // If the user didn't adjust the score but is moving on, treat the default par as the entered score.
            // This allows "tap next" to accept par without manually changing the score control.
            if (CurrentHole.Score == CurrentHole.Par)
            {
                CurrentHole.IsScored = true;
            }
        }

        await SaveCurrentHole();

        // If we were on the last hole, use this as a "finish" action.
        if (CurrentHoleIndex >= CurrentRound.Holes.Count - 1)
        {
            var confirm = await ShowRoundInfoAsync();
            if (confirm)
            {
                await CompleteRoundCoreAsync();
                return;
            }

            UpdateDisplay();
            return;
        }

        CurrentHoleIndex++;
        CurrentHole = CurrentRound.Holes[CurrentHoleIndex];
        EnsureStatsDefaults(CurrentHole);
        UpdateDisplay();
    }

    [RelayCommand]
    private async Task CompleteRound()
    {
        if (CurrentRound == null) return;

        // Save the last hole as-is (it may still be unscored)
        await SaveCurrentHole();

        var confirm = await ShowRoundInfoAsync();
        if (!confirm) return;

        await CompleteRoundCoreAsync();
    }

    private async Task CompleteRoundCoreAsync()
    {
        if (CurrentRound == null) return;

        try
        {
            IsBusy = true;
            CurrentRound.Status = RoundStatus.Completed;
            CurrentRound.EndTime = DateTime.Now;
            await _roundRepository.SaveItemAsync(CurrentRound);

            await Shell.Current.GoToAsync("..");
            var completedMessage = string.Format(AppResources.RoundCompleted, CurrentRound.TotalScore);
            await AppShell.DisplayToastAsync(completedMessage);
        }
        catch (Exception e)
        {
            _errorHandler.HandleError(e);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> ShowRoundInfoAsync()
    {
        if (CurrentRound == null) return false;

        var holes = CurrentRound.Holes
            .OrderBy(h => h.HoleNumber)
            .ToList();

        var scoredHoles = holes.Where(h => h.IsScored).ToList();

        int? front9 = null;
        int? back9 = null;

        if (holes.Count == 18)
        {
            var frontScored = scoredHoles.Where(h => h.HoleNumber is >= 1 and <= 9).ToList();
            var backScored = scoredHoles.Where(h => h.HoleNumber is >= 10 and <= 18).ToList();

            front9 = frontScored.Any() ? frontScored.Sum(h => h.Score) : null;
            back9 = backScored.Any() ? backScored.Sum(h => h.Score) : null;
        }

        var totalScore = scoredHoles.Any() ? scoredHoles.Sum(h => h.Score) : 0;

        var putts = scoredHoles.Sum(h => h.Putts ?? 0);
        var gir = scoredHoles.Count(h => h.GreenInRegulation == true);
        var penalties = scoredHoles.Count(h => h.Penalties > 0);

        var fairwayHit = scoredHoles.Count(h => h.FairwayResult == FairwayResult.Fairway);
        var fairwayLeft = scoredHoles.Count(h => h.FairwayResult == FairwayResult.Left);
        var fairwayRight = scoredHoles.Count(h => h.FairwayResult == FairwayResult.Right);

        var proxS = scoredHoles.Count(h => h.Proximity == 'S');
        var proxM = scoredHoles.Count(h => h.Proximity == 'M');
        var proxL = scoredHoles.Count(h => h.Proximity == 'L');

        var message = string.Format(
            AppResources.CompleteRoundMessage,
            CurrentRound.TotalScore,
            CurrentRound.ScoreDisplay);

        if (holes.Count == 18)
        {
            message += $"\n\nFront 9: {(front9.HasValue ? front9.Value.ToString() : "-")}" +
                       $"\nBack 9: {(back9.HasValue ? back9.Value.ToString() : "-")}" +
                       $"\nTotal: {totalScore}";
        }

        message += $"\n\nPutts: {putts}" +
                   $"\nGIR: {gir}" +
                   $"\nPenalty holes: {penalties}" +
                   $"\nFairways hit: {fairwayHit}" +
                   $"\nFairways left: {fairwayLeft}" +
                   $"\nFairways right: {fairwayRight}" +
                   $"\nProximity < 6ft: {proxS}" +
                   $"\nProximity 6–20ft: {proxM}" +
                   $"\nProximity > 20ft: {proxL}";

        return await Shell.Current.DisplayAlert(
            AppResources.CompleteRoundTitle,
            message,
            AppResources.Yes,
            AppResources.No);
    }

    [RelayCommand]
    private async Task PreviousHole()
    {
        if (CurrentHoleIndex <= 0) return;

        await SaveCurrentHole();
        CurrentHoleIndex--;
        CurrentHole = CurrentRound!.Holes[CurrentHoleIndex];
        EnsureStatsDefaults(CurrentHole);
        UpdateDisplay();
    }

    [RelayCommand]
    private async Task NavigateToHole(int holeIndex)
    {
        if (CurrentRound == null) return;
        if (holeIndex < 0 || holeIndex >= CurrentRound.Holes.Count) return;
        if (holeIndex == CurrentHoleIndex) return;

        await SaveCurrentHole();
        CurrentHoleIndex = holeIndex;
        CurrentHole = CurrentRound.Holes[CurrentHoleIndex];
        EnsureStatsDefaults(CurrentHole);
        UpdateDisplay();
    }

    [RelayCommand]
    private async Task AbandonRound()
    {
        if (CurrentRound == null) return;

        var confirm = await Shell.Current.DisplayAlert(
            AppResources.AbandonRoundTitle,
            AppResources.AbandonRoundMessage,
            AppResources.YesAbandon,
            AppResources.No);

        if (!confirm) return;

        try
        {
            IsBusy = true;
            CurrentRound.Status = RoundStatus.Abandoned;
            await _roundRepository.SaveItemAsync(CurrentRound);

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception e)
        {
            _errorHandler.HandleError(e);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveCurrentHole()
    {
        if (CurrentHole == null) return;

        EnsureStatsDefaults(CurrentHole);

        try
        {
            await _roundRepository.SaveHoleAsync(CurrentHole);

            if (CurrentRound != null)
            {
                CurrentRound.TotalScore = CurrentRound.Holes.Where(h => h.IsScored).Sum(h => h.Score);
                await _roundRepository.SaveItemAsync(CurrentRound);
                UpdateScoreDisplay();
            }
        }
        catch (Exception e)
        {
            _errorHandler.HandleError(e);
        }
    }

    private async Task SaveHoleOnlyAsync(Hole hole)
    {
        if (hole == null) return;

        EnsureStatsDefaults(hole);

        try
        {
            await _roundRepository.SaveHoleAsync(hole);

            if (CurrentRound != null)
            {
                CurrentRound.TotalScore = CurrentRound.Holes.Where(h => h.IsScored).Sum(h => h.Score);
                await _roundRepository.SaveItemAsync(CurrentRound);
                UpdateScoreDisplay();
            }
        }
        catch (Exception e)
        {
            _errorHandler.HandleError(e);
        }
    }

    [RelayCommand]
    private void TogglePenalty()
    {
        // no longer used (Penalty now binds to CurrentHole.Penalties via converter)
    }

    public string CurrentHoleNumberDisplay =>
        CurrentHole != null ? CurrentHole.HoleNumber.ToString() : "1";

    public string CurrentParNumberDisplay =>
        CurrentHole != null ? $"Par {CurrentHole.Par}" : "Par 4";

    private void UpdateDisplay()
    {
        OnPropertyChanged(nameof(TotalScoreDisplay));
        OnPropertyChanged(nameof(CourseParDisplay));
        OnPropertyChanged(nameof(TotalPutts));
        OnPropertyChanged(nameof(TotalGIR));
        OnPropertyChanged(nameof(HolesCompleted));
        OnPropertyChanged(nameof(CurrentHoleDisplay));
        OnPropertyChanged(nameof(CurrentParDisplay));
        OnPropertyChanged(nameof(CurrentHoleNumberDisplay));
        OnPropertyChanged(nameof(CurrentParNumberDisplay));
        OnPropertyChanged(nameof(CanDecreaseScore));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(IsLastHole));
        OnPropertyChanged(nameof(CanDecreasePutts));
        OnPropertyChanged(nameof(CanIncreasePutts));
    }

    private void UpdateScoreDisplay()
    {
        OnPropertyChanged(nameof(CurrentHole));
        OnPropertyChanged(nameof(CurrentHoleDisplay));
        OnPropertyChanged(nameof(CurrentParDisplay));
        OnPropertyChanged(nameof(CanDecreaseScore));
        OnPropertyChanged(nameof(CanDecreasePutts));
        OnPropertyChanged(nameof(CanIncreasePutts));

        if (CurrentRound != null)
        {
            CurrentRound.TotalScore = CurrentRound.Holes.Where(h => h.IsScored).Sum(h => h.Score);
            OnPropertyChanged(nameof(CurrentRound));
            OnPropertyChanged(nameof(TotalScoreDisplay));
            OnPropertyChanged(nameof(TotalPutts));
            OnPropertyChanged(nameof(TotalGIR));
        }
    }

    private void UpdatePuttsDisplay()
    {
        OnPropertyChanged(nameof(CurrentHole));
        OnPropertyChanged(nameof(CanDecreasePutts));
        OnPropertyChanged(nameof(CanIncreasePutts));
        OnPropertyChanged(nameof(TotalPutts));
        OnPropertyChanged(nameof(TotalGIR));
    }

    private CancellationTokenSource? _holeSaveCts;

    private async void CurrentHole_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not Hole hole) return;

        if (e.PropertyName is nameof(Hole.Score))
        {
            hole.IsScored = true;

            // Score affects putts max.
            OnPropertyChanged(nameof(CanDecreasePutts));
            OnPropertyChanged(nameof(CanIncreasePutts));
        }

        if (e.PropertyName is nameof(Hole.FairwayResult) or nameof(Hole.Penalties) or nameof(Hole.GreenInRegulation) or nameof(Hole.Putts) or nameof(Hole.Proximity))
        {
            EnsureStatsDefaults(hole);

            // Putts constraint: 0..Score; null allowed.
            if (hole.Putts.HasValue)
            {
                var clamped = Math.Clamp(hole.Putts.Value, 0, hole.Score);
                if (clamped != hole.Putts.Value)
                {
                    hole.Putts = clamped;
                }
            }

            _holeSaveCts?.Cancel();
            _holeSaveCts = new CancellationTokenSource();
            var token = _holeSaveCts.Token;

            try
            {
                await Task.Delay(150, token);
                await SaveHoleOnlyAsync(hole);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            OnPropertyChanged(nameof(CurrentHole));
            OnPropertyChanged(nameof(CanDecreasePutts));
            OnPropertyChanged(nameof(CanIncreasePutts));
            OnPropertyChanged(nameof(IsProximityS));
            OnPropertyChanged(nameof(IsProximityM));
            OnPropertyChanged(nameof(IsProximityL));
        }
    }

    public bool IsProximityS
    {
        get => CurrentHole?.Proximity == 'S';
        set
        {
            if (CurrentHole == null) return;
            if (!value) return;

            if (CurrentHole.Proximity != 'S')
            {
                CurrentHole.Proximity = 'S';
            }

            OnPropertyChanged(nameof(IsProximityS));
            OnPropertyChanged(nameof(IsProximityM));
            OnPropertyChanged(nameof(IsProximityL));
        }
    }

    public bool IsProximityM
    {
        get => CurrentHole?.Proximity == 'M';
        set
        {
            if (CurrentHole == null) return;
            if (!value) return;

            if (CurrentHole.Proximity != 'M')
            {
                CurrentHole.Proximity = 'M';
            }

            OnPropertyChanged(nameof(IsProximityS));
            OnPropertyChanged(nameof(IsProximityM));
            OnPropertyChanged(nameof(IsProximityL));
        }
    }

    public bool IsProximityL
    {
        get => CurrentHole?.Proximity == 'L';
        set
        {
            if (CurrentHole == null) return;
            if (!value) return;

            if (CurrentHole.Proximity != 'L')
            {
                CurrentHole.Proximity = 'L';
            }

            OnPropertyChanged(nameof(IsProximityS));
            OnPropertyChanged(nameof(IsProximityM));
            OnPropertyChanged(nameof(IsProximityL));
        }
    }
}
