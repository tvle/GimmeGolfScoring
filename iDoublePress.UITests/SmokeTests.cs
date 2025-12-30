using OpenQA.Selenium;

namespace iDoublePress.UITests;

public class SmokeTests : IClassFixture<AppiumFixture>
{
    private readonly AppiumFixture _fixture;

    public SmokeTests(AppiumFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void App_Launches()
    {
        _fixture.Driver.Should().NotBeNull();

        // Minimal smoke: verify a session exists and the app is responsive.
        // More robust assertions should rely on AutomationId once added to XAML.
        var source = _fixture.Driver!.PageSource;
        source.Should().NotBeNullOrWhiteSpace();
    }
}
