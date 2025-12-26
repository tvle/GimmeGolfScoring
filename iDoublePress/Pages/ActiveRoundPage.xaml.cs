using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class ActiveRoundPage : ContentPage
{
    public ActiveRoundPage(ActiveRoundPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
