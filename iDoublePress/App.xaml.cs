using Microsoft.Extensions.DependencyInjection;
using iDoublePress.Services;

namespace iDoublePress;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		
		// Initialize localization with saved language preference
		LocalizationManager.Instance.LoadSavedLanguage();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}