using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class AnalysisPage : ContentPage
{
    public AnalysisPage(AnalysisPageModel model)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AnalysisPage InitializeComponent failed: {ex}");
            Content = new Label
            {
                Text = $"Page load error: {ex.Message}",
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
        }

        BindingContext = model;
    }
}