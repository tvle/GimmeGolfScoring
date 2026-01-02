using Foundation;
using UIKit;

namespace iDoublePress;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	[Export("application:supportedInterfaceOrientationsForWindow:")]
	public UIInterfaceOrientationMask GetSupportedInterfaceOrientations(UIApplication application, nuint forWindow)
	{
		return UIInterfaceOrientationMask.Portrait;
	}
}
