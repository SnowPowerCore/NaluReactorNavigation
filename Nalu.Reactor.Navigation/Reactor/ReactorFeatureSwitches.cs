using System.Diagnostics.CodeAnalysis;

namespace Nalu.Reactor;

public class ReactorFeatureSwitches
{
    [FeatureSwitchDefinition("Nalu.Navigation.Reactor.HotReload")]
    internal static bool HotReloadIsEnabled =>
        AppContext.TryGetSwitch("Nalu.Navigation.Reactor.HotReload", out var isEnabled) && isEnabled;
}