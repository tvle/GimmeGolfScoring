using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;

namespace iDoublePress.PageModels;

public partial class RoundsPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;

    public ObservableCollection<Round> Rounds { get; } = new();

    public int RoundCount => Rounds.Count;

    [ObservableProperty]
    private bool isBusy;

    // Tracks the current sort direction: true = ascending, false = descending, null = no explicit sort
    private bool? _isSortAscending;

    public RoundsPageModel(RoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
        Rounds.CollectionChanged += (_, __) => OnPropertyChanged(nameof(RoundCount));
    }

    [RelayCommand]
    private async Task NavigatedToAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            Rounds.Clear();
            
            var rounds = await _roundRepository.ListAsync();
            foreach (var r in rounds)
            {
                Rounds.Add(r);
            }

            // Re-apply any previously selected sort so ordering is preserved when returning
            if (_isSortAscending.HasValue)
                ApplySort();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ViewRoundAsync(Round? round)
    {
        if (round is null)
            return;

        await Shell.Current.GoToAsync($"round-summary?roundId={round.ID}");
    }

    [RelayCommand]
    private async Task DeleteRoundAsync(Round? round)
    {
        if (round is null)
            return;

        var message = round.Status == RoundStatus.Completed 
            ? AppResources.DeleteCompletedRoundMessage 
            : AppResources.DeleteRoundMessage;

        var confirm = await Shell.Current.DisplayAlert(
            AppResources.DeleteRoundTitle,
            message,
            AppResources.Delete,
            AppResources.Cancel);

        if (!confirm)
            return;

        await _roundRepository.DeleteItemAsync(round);
        await NavigatedToAsync();
    }

    // Apply the current sort to the Rounds collection
    private void ApplySort()
    {
        if (!_isSortAscending.HasValue)
            return;

        List<Round> sorted;
        if (_isSortAscending.Value)
        {
            sorted = Rounds
                .OrderBy(r => r.StartTime)
                .ToList();
        }
        else
        {
            sorted = Rounds
                .OrderByDescending(r => r.StartTime)
                .ToList();
        }

        Rounds.Clear();
        foreach (var r in sorted)
            Rounds.Add(r);
    }

    [RelayCommand]
    private void SortByDateAsc()
    {
        _isSortAscending = true;
        ApplySort();
    }

    [RelayCommand]
    private void SortByDateDesc()
    {
        _isSortAscending = false;
        ApplySort();
    }
}