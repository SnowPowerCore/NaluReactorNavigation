using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TestReactorApp.Components;

namespace Nalu.Reactor.Navigation.Sample
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiReactorApp<AppShell>(unhandledExceptionAction: static e =>
                {
                    System.Diagnostics.Debug.WriteLine(e.ExceptionObject);
                })
                .ConfigureFonts(static fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder
                .UseNaluNavigation(configure: configurator =>
                {
                    configurator
                        .SetRoot<HomePage>()
                        .AddPage<SecondPage>()
                        .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger);
                });

            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            builder.Logging.AddConsole();

    #if DEBUG
            builder.Logging.AddDebug();
    #endif

            builder.Logging.AddEventSourceLogger();

            builder.Services.AddOptions();
            
            return builder.Build();
        }
    }
}