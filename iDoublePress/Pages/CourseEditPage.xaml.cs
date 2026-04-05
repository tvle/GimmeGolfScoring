using System.Globalization;
using iDoublePress.Models;
using iDoublePress.PageModels;
using iDoublePress.Resources.Strings;

namespace iDoublePress.Pages;

public partial class CourseEditPage : ContentPage
{
    private bool _isHandlingBackNavigation;

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

        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            Command = new Command(async () => await HandleBackNavigationAsync())
        });
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () => await HandleBackNavigationAsync());
        return true;
    }

    private async Task HandleBackNavigationAsync()
    {
        if (_isHandlingBackNavigation)
            return;

        if (BindingContext is not CourseEditPageModel vm)
            return;

        try
        {
            _isHandlingBackNavigation = true;

            if (vm.IsBusy)
                return;

            if (!vm.IsDirty)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            var discardText = GetString("Discard", "Discard");
            var action = await DisplayActionSheet(
                GetString("UnsavedChangesPrompt", "You have unsaved changes."),
                AppResources.Cancel,
                null,
                AppResources.Save,
                discardText);

            if (action == AppResources.Save)
            {
                await vm.SaveCourseAsync(navigateBack: true);
            }
            else if (action == discardText)
            {
                await Shell.Current.GoToAsync("..");
            }
        }
        finally
        {
            _isHandlingBackNavigation = false;
        }
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

    private static string GetString(string key, string fallback)
    {
        return AppResources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
    }
}
