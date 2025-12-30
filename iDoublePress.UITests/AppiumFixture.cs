using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Appium.Windows;

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
            AutomationName = GetAutomationName(platformName),
        };

        options.AddAdditionalAppiumOption(MobileCapabilityType.App, appPath);
        options.AddAdditionalAppiumOption("noReset", true);

        Driver = CreateDriver(new Uri(serverUrl), platformName, options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
    }

    private static string GetAutomationName(string platformName)
    {
        if (platformName.Equals("iOS", StringComparison.OrdinalIgnoreCase))
            return "XCUITest";

        if (platformName.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            return "Windows";

        return "UiAutomator2";
    }

    private static AppiumDriver CreateDriver(Uri serverUrl, string platformName, AppiumOptions options)
    {
        if (platformName.Equals("Android", StringComparison.OrdinalIgnoreCase))
            return new AndroidDriver(serverUrl, options);

        if (platformName.Equals("iOS", StringComparison.OrdinalIgnoreCase))
            return new IOSDriver(serverUrl, options);

        if (platformName.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            return new WindowsDriver(serverUrl, options);

        throw new InvalidOperationException($"Unsupported PLATFORM_NAME '{platformName}'. Use Android, iOS, or Windows.");
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
