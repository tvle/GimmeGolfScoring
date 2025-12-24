using CommunityToolkit.Mvvm.Input;
using iDoublePress.Models;

namespace iDoublePress.PageModels;

public interface IProjectTaskPageModel
{
	IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
	bool IsBusy { get; }
}