using iDoublePress.PageModels;
using Syncfusion.Maui.Toolkit.SegmentedControl;

namespace iDoublePress.Pages;

public partial class ActiveRoundPage : ContentPage
{
    public ActiveRoundPage(ActiveRoundPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        Loaded += (_, __) => ApplyFairwaySegmentTheme();
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += (_, __) => ApplyFairwaySegmentTheme();
        }
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
}
