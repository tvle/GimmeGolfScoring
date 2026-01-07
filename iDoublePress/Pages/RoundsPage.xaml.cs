using iDoublePress.Models;
using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class RoundsPage : ContentPage
{
    public RoundsPage(RoundsPageModel model)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RoundsPage InitializeComponent failed: {ex}");
            Content = new Label
            {
                Text = $"Page load error: {ex.Message}",
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
        }

        BindingContext = model;
    }

    private RoundsPageModel? Model => BindingContext as RoundsPageModel;

    private async void ViewButton_Clicked(object? sender, EventArgs e)
    {
        var round = (sender as Button)?.CommandParameter as Round;
        if (Model is null)
            return;

        await Model.ViewRoundCommand.ExecuteAsync(round);
    }

    private async void DeleteButton_Clicked(object? sender, EventArgs e)
    {
        var round = (sender as Button)?.CommandParameter as Round;
        if (Model is null)
            return;

        await Model.DeleteRoundCommand.ExecuteAsync(round);
    }
}