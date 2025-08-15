// ReSharper disable once CheckNamespace

using Nalu;

namespace Microsoft.Maui;

/// <summary>
/// Provides a fluent API for configuring Nalu navigation.
/// </summary>
public static class NaluMauiAppBuilderExtensions
{
    /// <summary>
    /// Adds Nalu navigation to the application.
    /// </summary>
    /// <param name="builder">Maui app builder.</param>
    /// <param name="configure">Navigation configurator.</param>
    public static MauiAppBuilder UseNaluNavigation(this MauiAppBuilder builder, Action<NavigationConfigurator> configure)
    {
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddScoped<INavigationServiceProviderInternal, NavigationServiceProvider>();
        builder.Services.AddScoped<INavigationServiceProvider>(sp => sp.GetRequiredService<INavigationServiceProviderInternal>());

        var configurator = new NavigationConfigurator(builder.Services);
        configure(configurator);

        return builder;
    }
}