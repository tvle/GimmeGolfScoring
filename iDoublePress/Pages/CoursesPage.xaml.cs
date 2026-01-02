using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class CoursesPage : ContentPage
{
    public CoursesPage(CoursesPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
