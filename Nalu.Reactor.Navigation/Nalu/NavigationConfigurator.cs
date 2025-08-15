using Nalu.Models;

namespace Nalu;

/// <summary>
/// Provides a fluent API for configuring Nalu navigation.
/// </summary>
public class NavigationConfigurator : INavigationConfiguration
{
    private readonly IServiceCollection _services;

    /// <inheritdoc />
    public ImageSource? MenuImage { get; private set; }

    /// <inheritdoc />
    public ImageSource? BackImage { get; private set; }

    /// <inheritdoc />
    public NavigationLeakDetectorState LeakDetectorState { get; private set; } = NavigationLeakDetectorState.EnabledWithDebugger;

    internal NavigationConfigurator(IServiceCollection services)
    {
        _services = services.AddSingleton<INavigationConfiguration>(this);
    }

    /// <summary>
    /// Sets the navigation leak detector state.
    /// </summary>
    /// <param name="state">Whether the leak detector should be enabled or not.</param>
    public NavigationConfigurator WithLeakDetectorState(NavigationLeakDetectorState state)
    {
        LeakDetectorState = state;

        return this;
    }

    /// <summary>
    /// Sets back navigation image.
    /// </summary>
    /// <param name="imageSource">Image to use for back navigation button.</param>
    public NavigationConfigurator WithBackImage(ImageSource imageSource)
    {
        BackImage = imageSource;

        return this;
    }

    /// <summary>
    /// Sets back navigation image.
    /// </summary>
    /// <param name="imageSource">Image to use for back navigation button.</param>
    public NavigationConfigurator WithMenuImage(ImageSource imageSource)
    {
        MenuImage = imageSource;

        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TPage" />.
    /// Adds <typeparamref name="TPage" /> as scoped services.
    /// </summary>
    /// <typeparam name="TPage">Type of the page.</typeparam>
    public NavigationConfigurator AddPage<TPage>() where TPage : MauiReactor.Component
    {
        _services.AddScoped<TPage>();
        
        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TPage" /> as root page.
    /// Adds <typeparamref name="TPage" /> as scoped services.
    /// </summary>
    /// <typeparam name="TPage">Type of the page.</typeparam>
    public NavigationConfigurator SetRoot<TPage>() where TPage : MauiReactor.Component
    {
        _services.AddScoped<TPage>();
        _services.AddScoped(sp => new RootTypeModel { Type = typeof(TPage) });

        return this;
    }
}