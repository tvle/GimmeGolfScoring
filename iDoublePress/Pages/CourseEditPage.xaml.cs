using iDoublePress.Models;
using iDoublePress.PageModels;

namespace iDoublePress.Pages;

public partial class CourseEditPage : ContentPage
{
    public CourseEditPage(CourseEditPageModel model)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CourseEditPage InitializeComponent failed: {ex}");
            Content = new Label
            {
                Text = $"Page load error: {ex.Message}",
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
        }

        BindingContext = model;
    }

    private void OnIncrementParClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not CourseEditPageModel vm)
            return;

        if (sender is not Button b)
            return;

        if (b.CommandParameter is CourseHole hole)
            vm.IncrementParCommand.Execute(hole);
    }

    private void OnDecrementParClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not CourseEditPageModel vm)
            return;

        if (sender is not Button b)
            return;

        if (b.CommandParameter is CourseHole hole)
            vm.DecrementParCommand.Execute(hole);
    }
}
