using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;
using LiveChartsCore.SkiaSharpView.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;
using LiveChartsCore.SkiaSharpView.Maui.Handlers;
using LiveChartsCore.SkiaSharpView;

namespace iDoublePress;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
            .UseSkiaSharp()
            .UseMauiCommunityToolkit()
			.ConfigureSyncfusionToolkit()
			.UseLiveCharts()
			.ConfigureMauiHandlers(handlers =>
			{
#if WINDOWS
				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
				{
					handler.PlatformView.SingleSelectionFollowsFocus = false;
				});
#endif
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
				fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
			});

#if DEBUG
		builder.Logging.AddDebug();
		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

		// Existing repositories
		builder.Services.AddSingleton<ModalErrorHandler>();
		
		// Golf repositories
		builder.Services.AddSingleton<PlayerRepository>();
		builder.Services.AddSingleton<CourseRepository>();
		builder.Services.AddSingleton<RoundRepository>();
		builder.Services.AddSingleton<GolfSeedDataService>();

		// Existing PageModels
		builder.Services.AddSingleton<MainPageModel>();

		// Golf PageModels
		builder.Services.AddSingleton<CoursesPageModel>();
		builder.Services.AddSingleton<RoundsPageModel>();
		builder.Services.AddTransient<CourseEditPageModel>();
		builder.Services.AddTransient<ActiveRoundPageModel>();
		// RoundSummaryPageModel is registered via AddTransientWithShellRoute below

		// Golf Pages
		builder.Services.AddTransientWithShellRoute<ActiveRoundPage, ActiveRoundPageModel>("active-round");
		builder.Services.AddTransientWithShellRoute<CoursesPage, CoursesPageModel>("courses");
		builder.Services.AddTransientWithShellRoute<RoundsPage, RoundsPageModel>("rounds");
		builder.Services.AddTransientWithShellRoute<CourseEditPage, CourseEditPageModel>("course-edit");
		builder.Services.AddTransientWithShellRoute<RoundSummaryPage, RoundSummaryPageModel>("round-summary");
		builder.Services.AddTransientWithShellRoute<AnalysisPage, AnalysisPageModel>("analysis");
		
		return builder.Build();
	}
}
