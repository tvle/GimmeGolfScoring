using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using iDoublePress.Models;
using iDoublePress.Resources.Strings;

namespace iDoublePress.Pages;

public enum InProgressRoundsResultAction
{
    Cancel,
    StartNew,
    Resume
}

public readonly record struct InProgressRoundsResult(InProgressRoundsResultAction Action, Round? Round);

public partial class InProgressRoundsPage : ContentPage
{
    private readonly Func<Round, Task> _deleteRound;
    private readonly TaskCompletionSource<InProgressRoundsResult> _tcs = new();

    public InProgressRoundsPage(List<Round> rounds, Func<Round, Task> deleteRound)
    {
        InitializeComponent();
        _deleteRound = deleteRound;
        BindingContext = new InProgressRoundsPageModel(rounds);
    }

    public Task<InProgressRoundsResult> GetResultAsync() => _tcs.Task;

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(new InProgressRoundsResult(InProgressRoundsResultAction.Cancel, null));
        await Navigation.PopModalAsync();
    }

    private void OnStartNewClicked(object? sender, EventArgs e)
    {
        // Do not dismiss this modal here. The caller will proceed to course selection;
        // keeping this page visible avoids bouncing back to the main screen.
        _tcs.TrySetResult(new InProgressRoundsResult(InProgressRoundsResultAction.StartNew, null));
    }

    private async void OnRoundTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not Round round)
            return;

        // Let the caller dismiss the modal. Dismissing here can race with additional navigation
        // on iOS (e.g., the caller also popping the modal before GoToAsync), leading to a crash.
        _tcs.TrySetResult(new InProgressRoundsResult(InProgressRoundsResultAction.Resume, round));
        await Task.CompletedTask;
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not Round round)
            return;

        var confirm = await DisplayAlert(
            AppResources.DeleteRoundTitle, 
            AppResources.DeleteRoundMessage, 
            AppResources.Delete, 
            AppResources.Cancel);
        if (!confirm)
            return;

        await _deleteRound(round);

        if (BindingContext is InProgressRoundsPageModel vm)
        {
            vm.Remove(round);

            if (vm.Rounds.Count == 0)
            {
                _tcs.TrySetResult(new InProgressRoundsResult(InProgressRoundsResultAction.StartNew, null));
            }
        }
    }
}

internal sealed class InProgressRoundsPageModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public InProgressRoundsPageModel(List<Round> rounds)
    {
        Rounds = new ObservableCollection<Round>(rounds);
        Rounds.CollectionChanged += (_, __) => OnPropertyChanged(nameof(TitleText));
    }

    public ObservableCollection<Round> Rounds { get; }

    public string TitleText => string.Format(AppResources.MultipleRoundsTitle, Rounds.Count);

    public void Remove(Round round)
    {
        Rounds.Remove(round);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
