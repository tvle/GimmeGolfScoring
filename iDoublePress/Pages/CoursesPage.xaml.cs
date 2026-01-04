using iDoublePress.Models;
using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class CoursesPage : ContentPage
{
    public CoursesPage(CoursesPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    private CoursesPageModel? Model => BindingContext as CoursesPageModel;

    private async void CopyButton_Clicked(object? sender, EventArgs e)
    {
        var courseWithLastPlayed = (sender as Button)?.CommandParameter as CourseWithLastPlayed;
        if (Model is null)
            return;

        await Model.CopyCourseCommand.ExecuteAsync(courseWithLastPlayed);
    }

    private async void EditButton_Clicked(object? sender, EventArgs e)
    {
        var courseWithLastPlayed = (sender as Button)?.CommandParameter as CourseWithLastPlayed;
        if (Model is null)
            return;

        await Model.EditCourseCommand.ExecuteAsync(courseWithLastPlayed);
    }

    private async void DeleteButton_Clicked(object? sender, EventArgs e)
    {
        var courseWithLastPlayed = (sender as Button)?.CommandParameter as CourseWithLastPlayed;
        if (Model is null)
            return;

        await Model.DeleteCourseCommand.ExecuteAsync(courseWithLastPlayed);
    }
}
