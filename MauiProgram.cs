using Microsoft.Extensions.Logging;
using EkadashiReminder.Pages;
using EkadashiReminder.Services;
using EkadashiReminder.ViewModels;
using Plugin.LocalNotification;

namespace EkadashiReminder
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseLocalNotification()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Services
            builder.Services.AddSingleton<EkadashiDataService>();
            builder.Services.AddSingleton<CustomReminderService>();
            builder.Services.AddSingleton<CalendarStoreService>();
            builder.Services.AddSingleton<NotificationService>();

            // ViewModels
            builder.Services.AddTransient<CalendarViewModel>();
            builder.Services.AddTransient<AddReminderViewModel>();

            // Pages
            builder.Services.AddTransient<CalendarPage>();
            builder.Services.AddTransient<RemindersPage>();
            builder.Services.AddTransient<AddReminderPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
