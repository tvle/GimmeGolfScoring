using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Appium.Windows;

namespace iDoublePress.UITests;

public sealed class AppiumFixture : IDisposable
{
    public AppiumDriver? Driver { get; private set; }

    public AppiumFixture()
    {
        var serverUrl = GetEnv("APPIUM_SERVER_URL") ?? "http://127.0.0.1:4723/";
        var platformName = GetEnv("PLATFORM_NAME") ?? "Android";
        var appPath = GetEnv("APP_PATH") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(appPath))
            throw new InvalidOperationException("Set APP_PATH to the built app package (.apk/.app/.msix/.exe). See iDoublePress.UITests/README.md");

        var options = new AppiumOptions
        {
            PlatformName = platformName,
            AutomationName = GetAutomationName(platformName),
            App = ExpandHome(appPath),
        };

        options.AddAdditionalAppiumOption("noReset", true);

        if (platformName.Equals("Android", StringComparison.OrdinalIgnoreCase))
        {
            // Helps with emulator/device selection and first-time bootstrap.
            var deviceName = GetEnv("DEVICE_NAME");
            if (!string.IsNullOrWhiteSpace(deviceName))
                options.DeviceName = deviceName;
            else
                options.DeviceName = "Android Emulator";

            var udid = GetEnv("UDID");
            if (!string.IsNullOrWhiteSpace(udid))
                options.AddAdditionalAppiumOption("udid", udid);

            // Appium Settings bootstrap can be slow on fresh emulators.
            options.AddAdditionalAppiumOption("settingsAppStartupTimeout", 120000);
            options.AddAdditionalAppiumOption("uiautomator2ServerLaunchTimeout", 120000);
            options.AddAdditionalAppiumOption("uiautomator2ServerInstallTimeout", 120000);
        }

        Driver = CreateDriver(new Uri(serverUrl), platformName, options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
    }

    private static string? GetEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        value = Environment.GetEnvironmentVariable("TEST_" + name);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        value = Environment.GetEnvironmentVariable("Test_" + name);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        return null;
    }

    private static string ExpandHome(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (path.StartsWith("~" + Path.DirectorySeparatorChar) || path is "~")
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.Length == 1)
                return home;

            return Path.Combine(home, path[2..]);
        }

        return path;
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
