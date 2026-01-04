using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class RoundSummaryPage : ContentPage
{
    public RoundSummaryPage(RoundSummaryPageModel viewModel)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RoundSummaryPage InitializeComponent failed: {ex}");
            Content = new Label
            {
                Text = $"Page load error: {ex.Message}",
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
        }
        BindingContext = viewModel;
    }
}
