using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Models;

namespace iDoublePress.PageModels;

[QueryProperty(nameof(RoundId), "roundId")]
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

    public string TotalScoreDisplay =>
        CurrentRound?.ScoreDisplay ?? "E";

    public string HolesCompleted =>
        CurrentRound != null ? $"{CurrentHoleIndex + 1}/{CurrentRound.Holes.Count}" : "0/18";

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

    private async Task LoadRound(int roundId)
    {
        try
        {
            IsBusy = true;
            CurrentRound = await _roundRepository.GetAsync(roundId);
            
            if (CurrentRound != null && CurrentRound.Holes.Any())
            {
                CurrentHole = CurrentRound.Holes[CurrentHoleIndex];
                UpdateDisplay();
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Round not found or has no holes", "OK");
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
    private async Task IncreaseScore()
    {
        if (CurrentHole == null) return;
        
        CurrentHole.Score++;
        await SaveCurrentHole();
        UpdateDisplay();
    }

    [RelayCommand]
    private async Task DecreaseScore()
    {
        if (CurrentHole == null || CurrentHole.Score <= 1) return;
        
        CurrentHole.Score--;
        await SaveCurrentHole();
        UpdateDisplay();
    }

    [RelayCommand]
    private async Task NextHole()
    {
        if (CurrentRound == null || CurrentHoleIndex >= CurrentRound.Holes.Count - 1) return;
        
        CurrentHoleIndex++;
        CurrentHole = CurrentRound.Holes[CurrentHoleIndex];
        UpdateDisplay();
    }

    [RelayCommand]
    private async Task PreviousHole()
    {
        if (CurrentHoleIndex <= 0) return;
        
        CurrentHoleIndex--;
        CurrentHole = CurrentRound!.Holes[CurrentHoleIndex];
        UpdateDisplay();
    }

    [RelayCommand]
    private async Task CompleteRound()
    {
        if (CurrentRound == null) return;

        var confirm = await Shell.Current.DisplayAlert(
            "Complete Round?",
            $"Your final score is {CurrentRound.TotalScore} ({CurrentRound.ScoreDisplay}). Mark this round as complete?",
            "Yes",
            "No");

        if (!confirm) return;

        try
        {
            IsBusy = true;
            CurrentRound.Status = RoundStatus.Completed;
            CurrentRound.EndTime = DateTime.Now;
            await _roundRepository.SaveItemAsync(CurrentRound);
            
            await Shell.Current.GoToAsync("..");
            await AppShell.DisplayToastAsync($"Round completed! Score: {CurrentRound.TotalScore}");
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
            "Abandon Round?",
            "Are you sure you want to abandon this round? It will not be saved.",
            "Yes, Abandon",
            "No");

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

        try
        {
            await _roundRepository.SaveHoleAsync(CurrentHole);
            
            if (CurrentRound != null)
            {
                CurrentRound.TotalScore = CurrentRound.Holes.Sum(h => h.Score);
                await _roundRepository.SaveItemAsync(CurrentRound);
            }
        }
        catch (Exception e)
        {
            _errorHandler.HandleError(e);
        }
    }

    private void UpdateDisplay()
    {
        OnPropertyChanged(nameof(TotalScoreDisplay));
        OnPropertyChanged(nameof(HolesCompleted));
        OnPropertyChanged(nameof(CanDecreaseScore));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(IsLastHole));
    }
}
