using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;

namespace iDoublePress.UITests;

public sealed class AppiumFixture : IDisposable
{
    public AppiumDriver? Driver { get; private set; }

    public AppiumFixture()
    {
        var serverUrl = Environment.GetEnvironmentVariable("APPIUM_SERVER_URL") ?? "http://127.0.0.1:4723/";
        var platformName = Environment.GetEnvironmentVariable("PLATFORM_NAME") ?? "Android";
        var appPath = Environment.GetEnvironmentVariable("APP_PATH") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(appPath))
            throw new InvalidOperationException("Set APP_PATH to the built app package (.apk/.app/.msix/.exe). See iDoublePress.UITests/README.md");

        var options = new AppiumOptions
        {
            PlatformName = platformName,
        };

        options.AddAdditionalAppiumOption(MobileCapabilityType.AutomationName,
            platformName.Equals("iOS", StringComparison.OrdinalIgnoreCase) ? "XCUITest" : "UiAutomator2");
        options.AddAdditionalAppiumOption(MobileCapabilityType.App, appPath);
        options.AddAdditionalAppiumOption("noReset", true);

        Driver = new AndroidDriver(new Uri(serverUrl), options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
    }

    public void Dispose()
    {
        try
        {
            Driver?.Quit();
            Driver?.Dispose();
        }
        finally
        {
            Driver = null;
        }
    }
}
