using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;

namespace iDoublePress;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureSyncfusionToolkit()
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
		builder.Services.AddTransient<ActiveRoundPageModel>();

		// Golf Pages
		builder.Services.AddTransientWithShellRoute<ActiveRoundPage, ActiveRoundPageModel>("active-round");
		
		return builder.Build();
	}
}
