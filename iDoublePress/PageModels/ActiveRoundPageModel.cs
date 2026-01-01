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
    public bool CanGoForward => CurrentRound != null && CurrentHoleIndex < CurrentRound.Holes.Count - 1;
    public bool IsLastHole => CurrentRound != null && CurrentHoleIndex == CurrentRound.Holes.Count - 1;

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
        if (CurrentRound == null || CurrentHoleIndex >= CurrentRound.Holes.Count - 1) return;

        if (CurrentHole != null)
        {
            CurrentHole.IsScored = true;
        }

        await SaveCurrentHole();
        CurrentHoleIndex++;
        CurrentHole = CurrentRound.Holes[CurrentHoleIndex];
        EnsureStatsDefaults(CurrentHole);
        UpdateDisplay();
    }

    [RelayCommand]
    private async Task PreviousHole()
    {
        if (CurrentHoleIndex <= 0) return;

        if (CurrentHole != null)
        {
            CurrentHole.IsScored = true;
        }

        await SaveCurrentHole();
        CurrentHoleIndex--;
        CurrentHole = CurrentRound!.Holes[CurrentHoleIndex];
        EnsureStatsDefaults(CurrentHole);
        UpdateDisplay();
    }

    [RelayCommand]
    private async Task CompleteRound()
    {
        if (CurrentRound == null) return;

        // Mark the current (last) hole as scored before completing
        if (CurrentHole != null)
        {
            CurrentHole.IsScored = true;
        }
        
        // Save the last hole
        await SaveCurrentHole();

        var message = string.Format(
            AppResources.CompleteRoundMessage,
            CurrentRound.TotalScore,
            CurrentRound.ScoreDisplay);

        var confirm = await Shell.Current.DisplayAlert(
            AppResources.CompleteRoundTitle,
            message,
            AppResources.Yes,
            AppResources.No);

        if (!confirm) return;

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
        OnPropertyChanged(nameof(HolesCompleted));
        OnPropertyChanged(nameof(CurrentHoleDisplay));
        OnPropertyChanged(nameof(CurrentParDisplay));
        OnPropertyChanged(nameof(CurrentHoleNumberDisplay));
        OnPropertyChanged(nameof(CurrentParNumberDisplay));
        OnPropertyChanged(nameof(CanDecreaseScore));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(IsLastHole));
    }

    private void UpdateScoreDisplay()
    {
        OnPropertyChanged(nameof(CurrentHole));
        OnPropertyChanged(nameof(CurrentHoleDisplay));
        OnPropertyChanged(nameof(CurrentParDisplay));
        OnPropertyChanged(nameof(CanDecreaseScore));

        if (CurrentRound != null)
        {
            CurrentRound.TotalScore = CurrentRound.Holes.Where(h => h.IsScored).Sum(h => h.Score);
            OnPropertyChanged(nameof(CurrentRound));
            OnPropertyChanged(nameof(TotalScoreDisplay));
        }
    }

    private CancellationTokenSource? _holeSaveCts;

    private async void CurrentHole_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not Hole hole) return;

        if (e.PropertyName is nameof(Hole.FairwayResult) or nameof(Hole.Penalties) or nameof(Hole.GreenInRegulation))
        {
            hole.IsScored = true;
            EnsureStatsDefaults(hole);

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
        }
    }
}
