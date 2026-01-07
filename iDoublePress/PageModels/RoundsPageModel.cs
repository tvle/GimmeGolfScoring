using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Data;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;
using System.Linq;
using System;

namespace iDoublePress.PageModels;

public partial class RoundsPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;

    public ObservableCollection<Round> Rounds { get; } = new();

    public int RoundCount => Rounds.Count;

    public string Title => string.Format(AppResources.RoundsCountFormat, RoundCount);

    [ObservableProperty]
    private bool isBusy;

    // Tracks the current sort direction: true = ascending, false = descending, null = no explicit sort
    private bool? _isSortAscending;

    private ObservableCollection<Round> _allRounds = new();

    public ObservableCollection<string> Years { get; } = new();

    [ObservableProperty]
    private string selectedYear;

    public RoundsPageModel(RoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
        Rounds.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(RoundCount));
            OnPropertyChanged(nameof(Title));
        };
        SelectedYear = DateTime.Now.Year.ToString();
        Years.Add(AppResources.All);
        Years.Add(DateTime.Now.Year.ToString());
    }

    [RelayCommand]
    private async Task NavigatedToAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            _allRounds.Clear();
            Rounds.Clear();
            
            var rounds = await _roundRepository.ListAsync();
            foreach (var r in rounds)
            {
                _allRounds.Add(r);
            }

            PopulateYears();

            // Ensure a valid SelectedYear exists in the Years collection after it is populated.
            var currentYearString = DateTime.Now.Year.ToString();
            if (Years.Contains(currentYearString))
            {
                SelectedYear = currentYearString;
            }
            else if (Years.Contains(AppResources.All))
            {
                SelectedYear = AppResources.All;
            }

            ApplyFilterAndSort();

            // Re-apply any previously selected sort so ordering is preserved when returning
            // Note: sort is now handled in ApplyFilterAndSort
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

        var confirm = false;

        if (Shell.Current is Shell shell)
        {
            confirm = await shell.DisplayAlertAsync(
                AppResources.DeleteRoundTitle,
                message,
                AppResources.Delete,
                AppResources.Cancel);
        }
        else
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
            {
                confirm = await page.DisplayAlertAsync(
                    AppResources.DeleteRoundTitle,
                    message,
                    AppResources.Delete,
                    AppResources.Cancel);
            }
        }

        if (!confirm)
            return;

        await _roundRepository.DeleteItemAsync(round);
        await NavigatedToAsync();
    }

    // Apply the current filter and sort to the Rounds collection
    private void ApplyFilterAndSort()
    {
        IEnumerable<Round> filtered = SelectedYear == AppResources.All ? _allRounds : _allRounds.Where(r => r.StartTime.Year.ToString() == SelectedYear);

        List<Round> sorted;
        if (_isSortAscending.HasValue)
        {
            if (_isSortAscending.Value)
            {
                sorted = filtered.OrderBy(r => r.StartTime).ToList();
            }
            else
            {
                sorted = filtered.OrderByDescending(r => r.StartTime).ToList();
            }
        }
        else
        {
            sorted = filtered.ToList();
        }

        Rounds.Clear();
        foreach (var r in sorted)
            Rounds.Add(r);
    }

    private void PopulateYears()
    {
        var years = _allRounds.Select(r => r.StartTime.Year).Distinct().ToList();
        years.Add(DateTime.Now.Year);
        var yearStrings = years.Distinct().OrderByDescending(y => y).Select(y => y.ToString()).ToList();

        Years.Clear();
        Years.Add(AppResources.All);
        foreach (var y in yearStrings)
            Years.Add(y);
    }

    [RelayCommand]
    private void SortByDateAsc()
    {
        _isSortAscending = true;
        ApplyFilterAndSort();
    }

    [RelayCommand]
    private void SortByDateDesc()
    {
        _isSortAscending = false;
        ApplyFilterAndSort();
    }

    partial void OnSelectedYearChanged(string value)
    {
        ApplyFilterAndSort();
    }
}