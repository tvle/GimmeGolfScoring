using iDoublePress.Models;
using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class ShowGPSPage : ContentPage
{
    public ShowGPSPage(ShowGPSPageModel viewModel)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowGPSPage InitializeComponent failed: {ex}");
            Content = new Label
            {
                Text = $"Page load error: {ex.Message}",
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
        }
        BindingContext = viewModel;
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
        // 1. Get the button that was clicked
        if (sender is Button button)
        {
            // 2. Retrieve the specific ShotSegment from the CommandParameter
            if (button.CommandParameter is ShotSegment segment)
            {
                // 3. Get the ViewModel from the page's BindingContext
                if (BindingContext is ShowGPSPageModel viewModel)
                {
                    // 4. Manually execute the command
                    // Note: We use the generated 'DeleteSegmentCommand' (public) 
                    // instead of the private 'DeleteSegment' method.
                    viewModel.DeleteSegmentCommand.Execute(segment);
                }
            }
        }
    }
}
