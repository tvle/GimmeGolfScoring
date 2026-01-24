using iDoublePress.PageModels;
using Syncfusion.Maui.Toolkit.SegmentedControl;

namespace iDoublePress.Pages;

public partial class ActiveRoundPage : ContentPage
{
    public ActiveRoundPage(ActiveRoundPageModel viewModel)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ActiveRoundPage InitializeComponent failed: {ex}");
            Content = new Label
            {
                Text = $"Page load error: {ex.Message}",
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
        }

        BindingContext = viewModel;

        Loaded += OnPageLoaded;
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += (_, __) => ApplyFairwaySegmentTheme();
        }
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        ApplyFairwaySegmentTheme();
    }

    private void ApplyFairwaySegmentTheme()
    {
        // x:Name fields for Syncfusion controls can be unreliable with certain source generators;
        // use FindByName as a fallback.
        var fh = this.FindByName<SfSegmentedControl>("FairwaySegmentedControl");
        if (fh == null)
            return;

        if (Application.Current?.Resources["Primary"] is not Color primary)
            return;

        // Toolkit 1.0.8 doesn't expose per-item text color APIs.
        // We can at least set control background/stroke to improve contrast.
        fh.Background = primary;
    }
    protected override bool OnBackButtonPressed()
    {
        // Invoke the ViewModel's NavigateBackCommand if available, otherwise fall back to default behavior.
        if (BindingContext is ActiveRoundPageModel vm && vm.NavigateBackCommand != null && vm.NavigateBackCommand.CanExecute(null))
        {
            vm.NavigateBackCommand.Execute(null);
            return true; // handled
        }

        return base.OnBackButtonPressed();
    }
}
