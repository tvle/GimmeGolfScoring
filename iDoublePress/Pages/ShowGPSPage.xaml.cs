using iDoublePress.Models;
using iDoublePress.PageModels;
using iDoublePress.Resources.Strings;

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
                Text = string.Format(AppResources.PageLoadErrorFormat, ex.Message),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
        }
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Stop the GPS location listener when leaving the page to conserve battery
        if (BindingContext is ShowGPSPageModel vm)
        {
            vm.StopLocationListener();
        }
    }

    private async void TagButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.BindingContext is ShotSegment segment && BindingContext is ShowGPSPageModel vm)
            {
                var options = new string[] {
                    AppResources.Tag_TeeBox, AppResources.Tag_D, AppResources.Tag_3W, AppResources.Tag_5W,
                    AppResources.Tag_3H, AppResources.Tag_4H, AppResources.Tag_4I, AppResources.Tag_5I,
                    AppResources.Tag_6I, AppResources.Tag_7I, AppResources.Tag_8I, AppResources.Tag_9I,
                    AppResources.Tag_PW, AppResources.Tag_GW, AppResources.Tag_SW, AppResources.Tag_LW,
                    AppResources.Tag_Front, AppResources.Tag_Center, AppResources.Tag_Back
                };
                var result = await DisplayActionSheet(AppResources.SelectTag, AppResources.Cancel, null, options);
                if (!string.IsNullOrEmpty(result) && result != AppResources.Cancel)
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
