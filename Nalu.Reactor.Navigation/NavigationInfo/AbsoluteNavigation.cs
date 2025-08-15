namespace Nalu;

#pragma warning disable SA1402
#pragma warning disable SA1649

/// <summary>
/// Represents the initial definition of an absolute navigation.
/// </summary>
public interface IAbsoluteNavigationInitialBuilder : INavigationInfo
{
    /// <summary>
    /// Navigates to <typeparamref name="TPage" /> root page using the specified type.
    /// </summary>
    /// <typeparam name="TPage">The type of page used on the `ShellContent`.</typeparam>
    IAbsoluteNavigationBuilder Root<TPage>() where TPage : class;

    /// <summary>
    /// Navigates to <typeparamref name="TPage" /> root page marked with a custom route.
    /// </summary>
    /// <param name="customRoute">The custom route defined on `Route` property of `ShellContent`.</param>
    /// <typeparam name="TPage">The type of page used on the `ShellContent`.</typeparam>
    IAbsoluteNavigationBuilder Root<TPage>(string customRoute) where TPage : class;
}

/// <summary>
/// Represents an absolute navigation.
/// </summary>
public interface IAbsoluteNavigationBuilder : INavigationInfo
{
    /// <summary>
    /// Adds a new page to the target navigation stack.
    /// </summary>
    /// <typeparam name="TPage">The page type to add.</typeparam>
    IAbsoluteNavigationBuilder Add<TPage>() where TPage : class;

    /// <summary>
    /// Sets the props delegate to be passed on the target page model.
    /// </summary>
    /// <param name="propsDelegate">The props delegate object.</param>
    INavigationInfo WithPropsDelegate<T>(Action<T>? propsDelegate);
}

/// <summary>
/// Defines an absolute navigation.
/// </summary>
public partial class AbsoluteNavigation : Navigation, IAbsoluteNavigationBuilder, IAbsoluteNavigationInitialBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AbsoluteNavigation" /> class.
    /// </summary>
    public AbsoluteNavigation() : base(true, default) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AbsoluteNavigation" /> class with the specified behavior.
    /// </summary>
    /// <param name="behavior">Custom navigation behavior.</param>
    public AbsoluteNavigation(NavigationBehavior? behavior) : base(true, behavior) { }

    /// <inheritdoc />
    public IAbsoluteNavigationBuilder ShellContent<TPage>() where TPage : class =>
        Root<TPage>();

    /// <inheritdoc />
    public IAbsoluteNavigationBuilder ShellContent<TPage>(string customRoute) where TPage : class =>
        Root<TPage>(customRoute);

    /// <inheritdoc />
    public IAbsoluteNavigationBuilder Root<TPage>() where TPage : class
    {
        if (Count != 0)
        {
            throw new InvalidOperationException("Cannot add a shell content on top of another one.");
        }

        Add(new NavigationSegment
        {
            Type = typeof(TPage)
        });

        return this;
    }

    /// <inheritdoc />
    public IAbsoluteNavigationBuilder Root<TPage>(string customRoute) where TPage : class
    {
        if (Count != 0)
        {
            throw new InvalidOperationException("Cannot add a shell content on top of another one.");
        }

        Add(new NavigationSegment
        {
            Type = typeof(TPage),
            SegmentName = customRoute
        });

        return this;
    }

    /// <inheritdoc />
    public IAbsoluteNavigationBuilder Add<TPage>() where TPage : class
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("Cannot add a page without adding a shell content first.");
        }

        Add(new NavigationSegment
        {
            Type = typeof(TPage)
        });

        return this;
    }

    /// <inheritdoc />
    public INavigationInfo WithPropsDelegate<T>(Action<T>? propsDelegate)
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("Cannot set props delegate on an empty navigation.");
        }

        PropsDelegate = o => propsDelegate((T)o);

        return this;
    }
}