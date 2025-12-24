using iDoublePress.Models;
using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class MainPage : ContentPage
{
	public MainPage(MainPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}