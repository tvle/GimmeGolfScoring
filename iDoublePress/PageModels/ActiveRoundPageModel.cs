using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;
using iDoublePress.Utilities;
using System.Threading.Tasks;

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
    private async Task NavigateBack()
    {
        // Use the generated public command property so the command is lazily initialized by the source generator.
        // Fall back to calling the handler directly if the command is not available.
        if (AbandonRoundCommand?.CanExecute(null) == true)
        {
            await AbandonRoundCommand.ExecuteAsync(null);
        }
        else
        {
            await AbandonRound();
        }

        await Shell.Current.GoToAsync("..");
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

    [RelayCommand]
    private async Task ShowGPSPage()
    {
        if (CurrentHole == null) return;

        await NavigateToShowGPSPage();
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
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.RoundNotFound, AppResources.OK);
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
            await NavigateToRoundSummary();
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

        await NavigateToRoundSummary();
    }

    private async Task NavigateToRoundSummary()
    {
        if (CurrentRound == null) return;

        await Shell.Current.GoToAsync($"round-summary?roundId={CurrentRound.ID}");
    }
    private async Task NavigateToShowGPSPage()
    {
        if (CurrentRound == null) return;

        await Shell.Current.GoToAsync($"show-gps?roundId={CurrentRound.ID}&holeIndex={CurrentHoleIndex}");
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

        var confirm = await Shell.Current.DisplayAlertAsync(
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
        CurrentHole != null ? string.Format(AppResources.ParFormat, CurrentHole.Par) : string.Format(AppResources.ParFormat, 4);

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

    private readonly Debouncer _holeSaveDebouncer;

    public ActiveRoundPageModel(RoundRepository roundRepository, ModalErrorHandler errorHandler)
    {
        _roundRepository = roundRepository;
        _errorHandler = errorHandler;
        _holeSaveDebouncer = new Debouncer(TimeSpan.FromMilliseconds(150));
    }

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

            // Use debouncer to prevent race conditions during rapid input
            _holeSaveDebouncer.Debounce(async () =>
            {
                await SaveHoleOnlyAsync(hole);
            });

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
    private async void OnDisappearing()
    {
        // Clean up debouncer to prevent memory leaks
        _holeSaveDebouncer.Dispose();
        await Task.CompletedTask;
    }
}