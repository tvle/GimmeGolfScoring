using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Models;
using iDoublePress.Pages;
using iDoublePress.Resources.Strings;

namespace iDoublePress.PageModels;

public partial class MainPageModel : ObservableObject
{
	private bool _isNavigatedTo;
	private bool _dataLoaded;
    private readonly ModalErrorHandler _errorHandler; 
	private readonly GolfSeedDataService _golfSeedDataService;
	private readonly PlayerRepository _playerRepository;
	private readonly CourseRepository _courseRepository;
	private readonly RoundRepository _roundRepository;

	[ObservableProperty]
	private List<Round> _recentRounds = [];

	[ObservableProperty]
	private Round? selectedRecentRound;

	[ObservableProperty]
	bool _isBusy;

	[ObservableProperty]
	bool _isRefreshing;

	[ObservableProperty]
	private string _today = DateTime.Now.ToString("dddd, MMM d");

	public MainPageModel(GolfSeedDataService golfSeedDataService, PlayerRepository playerRepository, CourseRepository courseRepository, RoundRepository roundRepository, 
						ModalErrorHandler errorHandler)
	{
        _errorHandler = errorHandler;
        _golfSeedDataService = golfSeedDataService;
		_playerRepository = playerRepository;
		_courseRepository = courseRepository;
		_roundRepository = roundRepository;
	}

	private async Task LoadData()
	{
		try
		{
			IsBusy = true;


			// Load recent golf rounds - wrap in try-catch to prevent crashes
			try
			{
				var allRounds = await _roundRepository.ListAsync();
				RecentRounds = allRounds
					.Where(r => r.Status == RoundStatus.Completed)
					.OrderByDescending(r => r.StartTime)
					.Take(5)
					.ToList();
			}
			catch (Exception ex)
			{
				// If golf data fails to load, just leave it empty
				RecentRounds = new List<Round>();
				System.Diagnostics.Debug.WriteLine($"Failed to load golf rounds: {ex.Message}");
			}
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task InitData(GolfSeedDataService seedDataService)
	{
		// Initialize golf data
		bool isGolfSeeded = Preferences.Default.ContainsKey("is_golf_seeded");
		if (!isGolfSeeded)
		{
			await _golfSeedDataService.LoadSeedDataAsync();
			Preferences.Default.Set("is_golf_seeded", true);
		}

		await Refresh();
	}

	[RelayCommand]
	private async Task Refresh()
	{
		try
		{
			IsRefreshing = true;
			await LoadData();
		}
		catch (Exception e)
		{
			_errorHandler.HandleError(e);
		}
		finally
		{
			IsRefreshing = false;
		}
	}

	[RelayCommand]
	private void NavigatedTo() =>
		_isNavigatedTo = true;

	[RelayCommand]
	private void NavigatedFrom() =>
		_isNavigatedTo = false;

	[RelayCommand]
	private async Task Appearing()
	{
		if (!_dataLoaded)
		{
			await InitData(_golfSeedDataService);
			_dataLoaded = true;
			await Refresh();
		}
		// This means we are being navigated to
		else if (!_isNavigatedTo)
		{
			await Refresh();
		}
	}



	[RelayCommand]
	private async Task StartNewRound()
	{
		try
		{
			IsBusy = true;

			// Get default player
			var players = await _playerRepository.ListAsync();
			var player = players.FirstOrDefault();
			
			if (player == null)
			{
				_errorHandler.HandleError(new Exception(AppResources.NoPlayerFound));
				return;
			}

			// Check for in-progress rounds (plural)
			var inProgressRounds = await _roundRepository.GetInProgressRoundsAsync(player.ID);
			if (inProgressRounds.Any())
			{
				if (inProgressRounds.Count == 1)
				{
					// Single in-progress round - show simple dialog
					var inProgressRound = inProgressRounds[0];
					var timeAgo = GetTimeAgo(inProgressRound.StartTime);
					var holesCompleted = inProgressRound.Holes.Count(h => h.IsScored);
					var totalHoles = inProgressRound.Holes.Count;
					
					var message = string.Format(
						AppResources.ResumeRoundMessage,
						inProgressRound.Course?.Name,
						timeAgo,
						holesCompleted,
						totalHoles);
					
					var resume = await Shell.Current.DisplayAlert(
						AppResources.ResumeRoundTitle,
						message,
						AppResources.Resume,
						AppResources.StartNew);

					if (resume)
					{
						var nextHoleIndex = inProgressRound.Holes.FindIndex(h => !h.IsScored);
						if (nextHoleIndex < 0) nextHoleIndex = 0;
						await Shell.Current.GoToAsync($"active-round?roundId={inProgressRound.ID}&holeIndex={nextHoleIndex}");
						return;
					}
				}
				else
				{
					// Multiple in-progress rounds - show custom picker UI that supports per-item delete
					var picker = new InProgressRoundsPage(inProgressRounds, async roundToDelete =>
					{
						await _roundRepository.DeleteItemAsync(roundToDelete);
					});

					await Shell.Current.Navigation.PushModalAsync(picker);
					var result = await picker.GetResultAsync();

					if (result.Action == InProgressRoundsResultAction.Cancel)
						return;

					if (result.Action == InProgressRoundsResultAction.StartNew)
					{
						// Close the modal before presenting the course selection sheet.
						await Shell.Current.Navigation.PopModalAsync();
						await Task.Delay(50);
						// Continue to course selection below
					}
					else if (result.Action == InProgressRoundsResultAction.Resume && result.Round != null)
					{
						var nextHoleIndex = result.Round.Holes.FindIndex(h => !h.IsScored);
						if (nextHoleIndex < 0) nextHoleIndex = 0;
						await Shell.Current.GoToAsync($"active-round?roundId={result.Round.ID}&holeIndex={nextHoleIndex}");
						return;
					}
				}
			}

			// Show course selection
			var courses = await _courseRepository.ListAsync();
			var courseNames = courses.Select(c => c.Name).ToArray();
			
			var selectedCourse = await Shell.Current.DisplayActionSheet(
				AppResources.SelectCourse,
				AppResources.Cancel,
				null,
				courseNames);

			if (selectedCourse == AppResources.Cancel || string.IsNullOrEmpty(selectedCourse))
				return;

			var course = courses.FirstOrDefault(c => c.Name == selectedCourse);
			if (course == null)
				return;

			// Create new round
			var newRound = await _roundRepository.CreateNewRoundAsync(player.ID, course.ID);
			
			// Navigate to active round page
			await Shell.Current.GoToAsync($"active-round?roundId={newRound.ID}");
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
	private async Task NavigateToRound(object? parameter)
	{
		var round = parameter as Round ?? SelectedRecentRound;
		if (round == null) return;

		await Shell.Current.GoToAsync($"active-round?roundId={round.ID}");

		SelectedRecentRound = null;
	}

	private string GetTimeAgo(DateTime startTime)
	{
		var timeSpan = DateTime.Now - startTime;
		
		if (timeSpan.TotalMinutes < 1)
			return AppResources.JustNow;
		if (timeSpan.TotalMinutes < 60)
			return string.Format(AppResources.MinutesAgo, (int)timeSpan.TotalMinutes);
		if (timeSpan.TotalHours < 24)
		{
			var hours = (int)timeSpan.TotalHours;
			return hours == 1 
				? string.Format(AppResources.HourAgo, hours)
				: string.Format(AppResources.HoursAgo, hours);
		}
		if (timeSpan.TotalDays < 7)
		{
			var days = (int)timeSpan.TotalDays;
			return days == 1
				? string.Format(AppResources.DayAgo, days)
				: string.Format(AppResources.DaysAgo, days);
		}
		
		return startTime.ToString("MMM d, h:mm tt");
	}
}