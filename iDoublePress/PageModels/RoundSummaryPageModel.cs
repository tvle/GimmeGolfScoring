using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;

namespace iDoublePress.PageModels;

[QueryProperty(nameof(RoundId), "roundId")]
public partial class RoundSummaryPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    private readonly ModalErrorHandler _errorHandler;

    [ObservableProperty]
    private int roundId;

    [ObservableProperty]
    private Round? currentRound;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private int totalScore;

    [ObservableProperty]
    private string scoreDisplay = "E";

    [ObservableProperty]
    private int? front9Score;

    [ObservableProperty]
    private int? back9Score;

    [ObservableProperty]
    private int putts;

    [ObservableProperty]
    private int gir;

    [ObservableProperty]
    private int penaltyHoles;

    [ObservableProperty]
    private int fairwaysHit;

    [ObservableProperty]
    private int fairwaysLeft;

    [ObservableProperty]
    private int fairwaysRight;

    [ObservableProperty]
    private int proximityShort;

    [ObservableProperty]
    private int proximityMedium;

    [ObservableProperty]
    private int proximityLong;

    [ObservableProperty]
    private bool is18Holes;

    public RoundSummaryPageModel(RoundRepository roundRepository, ModalErrorHandler errorHandler)
    {
        _roundRepository = roundRepository;
        _errorHandler = errorHandler;
    }

    async partial void OnRoundIdChanged(int value)
    {
        try
        {
            if (value > 0)
            {
                await LoadRoundSummary(value);
            }
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    private async Task LoadRoundSummary(int roundId)
    {
        try
        {
            IsBusy = true;
            CurrentRound = await _roundRepository.GetAsync(roundId);

            if (CurrentRound == null)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.RoundNotFound, AppResources.OK);
                await Shell.Current.GoToAsync("..");
                return;
            }

            CalculateStats();
        }
        catch (Exception e)
        {
            _errorHandler.HandleError(e);
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch
            {
                // Ignore navigation errors during error handling
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CalculateStats()
    {
        if (CurrentRound == null) return;

        var holes = CurrentRound.Holes.OrderBy(h => h.HoleNumber).ToList();
        var scoredHoles = holes.Where(h => h.IsScored).ToList();

        Is18Holes = holes.Count == 18;

        if (Is18Holes)
        {
            var frontScored = scoredHoles.Where(h => h.HoleNumber is >= 1 and <= 9).ToList();
            var backScored = scoredHoles.Where(h => h.HoleNumber is >= 10 and <= 18).ToList();

            Front9Score = frontScored.Any() ? frontScored.Sum(h => h.Score) : null;
            Back9Score = backScored.Any() ? backScored.Sum(h => h.Score) : null;
        }

        TotalScore = scoredHoles.Any() ? scoredHoles.Sum(h => h.Score) : 0;
        ScoreDisplay = CurrentRound.ScoreDisplay;

        Putts = scoredHoles.Sum(h => h.Putts ?? 0);
        Gir = scoredHoles.Count(h => h.GreenInRegulation == true);
        PenaltyHoles = scoredHoles.Count(h => h.Penalties > 0);

        FairwaysHit = scoredHoles.Count(h => h.FairwayResult == FairwayResult.Fairway);
        FairwaysLeft = scoredHoles.Count(h => h.FairwayResult == FairwayResult.Left);
        FairwaysRight = scoredHoles.Count(h => h.FairwayResult == FairwayResult.Right);

        ProximityShort = scoredHoles.Count(h => h.Proximity == 'S');
        ProximityMedium = scoredHoles.Count(h => h.Proximity == 'M');
        ProximityLong = scoredHoles.Count(h => h.Proximity == 'L');
    }

    [RelayCommand]
    private async Task CompleteRound()
    {
        if (CurrentRound == null) return;

        try
        {
            IsBusy = true;
            CurrentRound.Status = RoundStatus.Completed;
            CurrentRound.EndTime = DateTime.Now;
            await _roundRepository.SaveItemAsync(CurrentRound);

            await Shell.Current.GoToAsync("../..");
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
    private async Task GoBack()
    {
        await Shell.Current.GoToAsync("..");
    }
}
