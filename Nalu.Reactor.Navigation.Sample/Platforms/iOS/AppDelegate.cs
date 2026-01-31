using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace Nalu.Reactor.Navigation.Sample
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
