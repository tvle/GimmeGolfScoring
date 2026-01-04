using Microsoft.Extensions.DependencyInjection;
using iDoublePress.Services;

namespace iDoublePress;

public partial class App : Application
{
	public App()
	{
		// Add global exception handlers
		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			System.Diagnostics.Debug.WriteLine($"AppDomain UnhandledException: {e.ExceptionObject}");
		};
		
		TaskScheduler.UnobservedTaskException += (s, e) =>
		{
			System.Diagnostics.Debug.WriteLine($"UnobservedTaskException: {e.Exception}");
			e.SetObserved();
		};

		InitializeComponent();
		
		// Initialize localization with saved language preference
		LocalizationManager.Instance.LoadSavedLanguage();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}