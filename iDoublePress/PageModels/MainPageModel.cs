using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iDoublePress.Models;

namespace iDoublePress.PageModels;

public partial class MainPageModel : ObservableObject, IProjectTaskPageModel
{
	private bool _isNavigatedTo;
	private bool _dataLoaded;
	private readonly ProjectRepository _projectRepository;
	private readonly TaskRepository _taskRepository;
	private readonly CategoryRepository _categoryRepository;
	private readonly ModalErrorHandler _errorHandler;
	private readonly SeedDataService _seedDataService;
	private readonly GolfSeedDataService _golfSeedDataService;
	private readonly PlayerRepository _playerRepository;
	private readonly CourseRepository _courseRepository;
	private readonly RoundRepository _roundRepository;

	[ObservableProperty]
	private List<CategoryChartData> _todoCategoryData = [];

	[ObservableProperty]
	private List<Brush> _todoCategoryColors = [];

	[ObservableProperty]
	private List<ProjectTask> _tasks = [];

	[ObservableProperty]
	private List<Project> _projects = [];

	[ObservableProperty]
	private List<Round> _recentRounds = [];

	[ObservableProperty]
	bool _isBusy;

	[ObservableProperty]
	bool _isRefreshing;

	[ObservableProperty]
	private string _today = DateTime.Now.ToString("dddd, MMM d");

	[ObservableProperty]
	private Project? selectedProject;

	public bool HasCompletedTasks
		=> Tasks?.Any(t => t.IsCompleted) ?? false;

	public MainPageModel(SeedDataService seedDataService, GolfSeedDataService golfSeedDataService,
		ProjectRepository projectRepository, TaskRepository taskRepository, 
		CategoryRepository categoryRepository, ModalErrorHandler errorHandler,
		PlayerRepository playerRepository, CourseRepository courseRepository, RoundRepository roundRepository)
	{
		_projectRepository = projectRepository;
		_taskRepository = taskRepository;
		_categoryRepository = categoryRepository;
		_errorHandler = errorHandler;
		_seedDataService = seedDataService;
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

			Projects = await _projectRepository.ListAsync();

			var chartData = new List<CategoryChartData>();
			var chartColors = new List<Brush>();

			var categories = await _categoryRepository.ListAsync();
			foreach (var category in categories)
			{
				chartColors.Add(category.ColorBrush);

				var ps = Projects.Where(p => p.CategoryID == category.ID).ToList();
				int tasksCount = ps.SelectMany(p => p.Tasks).Count();

				chartData.Add(new(category.Title, tasksCount));
			}

			TodoCategoryData = chartData;
			TodoCategoryColors = chartColors;

			Tasks = await _taskRepository.ListAsync();

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
			OnPropertyChanged(nameof(HasCompletedTasks));
		}
	}

	private async Task InitData(SeedDataService seedDataService)
	{
		bool isSeeded = Preferences.Default.ContainsKey("is_seeded");

		if (!isSeeded)
		{
			await seedDataService.LoadSeedDataAsync();
		}

		Preferences.Default.Set("is_seeded", true);

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
			await InitData(_seedDataService);
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
	private Task TaskCompleted(ProjectTask task)
	{
		OnPropertyChanged(nameof(HasCompletedTasks));
		return _taskRepository.SaveItemAsync(task);
	}

	[RelayCommand]
	private Task AddTask()
		=> Shell.Current.GoToAsync($"task");

	[RelayCommand]
	private Task? NavigateToProject(Project project)
		=> project is null ? null : Shell.Current.GoToAsync($"project?id={project.ID}");

	[RelayCommand]
	private Task NavigateToTask(ProjectTask task)
		=> Shell.Current.GoToAsync($"task?id={task.ID}");

	[RelayCommand]
	private async Task CleanTasks()
	{
		var completedTasks = Tasks.Where(t => t.IsCompleted).ToList();
		foreach (var task in completedTasks)
		{
			await _taskRepository.DeleteItemAsync(task);
			Tasks.Remove(task);
		}

		OnPropertyChanged(nameof(HasCompletedTasks));
		Tasks = new(Tasks);
		await AppShell.DisplayToastAsync("All cleaned up!");
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
				_errorHandler.HandleError(new Exception("No player found. Please restart the app."));
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
					
					var resume = await Shell.Current.DisplayAlert(
						"Resume Round?",
						$"You have a round in progress at {inProgressRound.Course?.Name}.\n" +
						$"Started: {timeAgo}\n" +
						$"Progress: {holesCompleted}/{totalHoles} holes\n\n" +
						$"Resume it?",
						"Resume",
						"Start New");

					if (resume)
					{
						await Shell.Current.GoToAsync($"active-round?roundId={inProgressRound.ID}");
						return;
					}
				}
				else
				{
					// Multiple in-progress rounds - show action sheet with details
					var options = new List<string>();
					foreach (var r in inProgressRounds)
					{
						var timeAgo = GetTimeAgo(r.StartTime);
						var holesCompleted = r.Holes.Count(h => h.IsScored);
						var totalHoles = r.Holes.Count;
						options.Add($"{r.Course?.Name} - {timeAgo} ({holesCompleted}/{totalHoles})");
					}
					options.Add("Start New Round");

					var selected = await Shell.Current.DisplayActionSheet(
						$"You have {inProgressRounds.Count} rounds in progress",
						"Cancel",
						null,
						options.ToArray());

					if (selected == "Cancel" || string.IsNullOrEmpty(selected))
						return;

					if (selected == "Start New Round")
					{
						// Continue to course selection below
					}
					else
					{
						// Find and resume the selected round
						var selectedIndex = options.IndexOf(selected);
						if (selectedIndex >= 0 && selectedIndex < inProgressRounds.Count)
						{
							await Shell.Current.GoToAsync($"active-round?roundId={inProgressRounds[selectedIndex].ID}");
							return;
						}
					}
				}
			}

			// Show course selection
			var courses = await _courseRepository.ListAsync();
			var courseNames = courses.Select(c => c.Name).ToArray();
			
			var selectedCourse = await Shell.Current.DisplayActionSheet(
				"Select Course",
				"Cancel",
				null,
				courseNames);

			if (selectedCourse == "Cancel" || string.IsNullOrEmpty(selectedCourse))
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
	private async Task NavigateToRound(Round round)
	{
		if (round == null) return;

		if (round.Status == RoundStatus.InProgress)
		{
			await Shell.Current.GoToAsync($"active-round?roundId={round.ID}");
		}
		else
		{
			// For now, just navigate to active round in view-only mode
			// In Phase 2/3 we'll add a dedicated round detail/summary page
			await Shell.Current.GoToAsync($"active-round?roundId={round.ID}");
		}
	}

	private string GetTimeAgo(DateTime startTime)
	{
		var timeSpan = DateTime.Now - startTime;
		
		if (timeSpan.TotalMinutes < 1)
			return "Just now";
		if (timeSpan.TotalMinutes < 60)
			return $"{(int)timeSpan.TotalMinutes} min ago";
		if (timeSpan.TotalHours < 24)
			return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours > 1 ? "s" : "")} ago";
		if (timeSpan.TotalDays < 7)
			return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays > 1 ? "s" : "")} ago";
		
		return startTime.ToString("MMM d, h:mm tt");
	}
}