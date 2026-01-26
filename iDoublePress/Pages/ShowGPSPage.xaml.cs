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

    private async void TagButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.BindingContext is ShotSegment segment && BindingContext is ShowGPSPageModel vm)
            {
                var options = new string[] { "Tee Box", "D", "3W", "5W", "3H", "4H", "4I", "5I", "6I", "7I", "8I", "9I", "PW", "GW", "SW", "LW", "Front", "Center", "Back" };
                var result = await DisplayActionSheet("Select Tag", "Cancel", null, options);
                if (!string.IsNullOrEmpty(result) && result != "Cancel")
                {
                    segment.Tag = result;
                    // Persist via ViewModel
                    await vm.PersistShotSegmentsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tag selection failed: {ex}");
        }

    }

    private void DeleteButton_Clicked(object sender, EventArgs e)
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
