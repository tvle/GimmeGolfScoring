using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class ImportCoursePage : ContentPage
{
    public ImportCoursePage(ImportCoursePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}