using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Enums;

namespace iDoublePress.UITests;

public sealed class AppiumFixture : IAsyncLifetime
{
    public AppiumDriver? Driver { get; private set; }

    public async Task InitializeAsync()
    {
        var serverUrl = Environment.GetEnvironmentVariable("APPIUM_SERVER_URL") ?? "http://127.0.0.1:4723/";
        var platformName = Environment.GetEnvironmentVariable("PLATFORM_NAME") ?? "Android";
        var appPath = Environment.GetEnvironmentVariable("APP_PATH") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(appPath))
            throw new InvalidOperationException("Set APP_PATH to the built app package (.apk/.app/.msix/.exe). See iDoublePress.UITests/README.md");

        var options = new AppiumOptions();
        options.PlatformName = platformName;

        options.AddAdditionalAppiumOption(MobileCapabilityType.AutomationName, platformName.Equals("iOS", StringComparison.OrdinalIgnoreCase) ? "XCUITest" : "UiAutomator2");
        options.AddAdditionalAppiumOption(MobileCapabilityType.App, appPath);

        // Keep the app stable between tests; individual tests can reset if needed.
        options.AddAdditionalAppiumOption("noReset", true);

        Driver = new AppiumDriver(new Uri(serverUrl), options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
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

        await Task.CompletedTask;
    }
}
