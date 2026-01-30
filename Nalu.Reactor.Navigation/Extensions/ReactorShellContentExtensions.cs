using System.Reflection;
using MauiReactor;

namespace Nalu;

/// <summary>
/// Extensions for configuring MauiReactor ShellContent with NaluShell specifics.
/// </summary>
public static class ReactorShellContentExtensions
{
    /// <summary>
    /// Configures a ShellContent with RenderContent factory and automatically applies NaluShell PageType.
    /// This allows using only RenderContent() without needing to call .Set(Nalu.Navigation.PageTypeProperty, ...).
    /// </summary>
    /// <typeparam name="TPage">The type of the page component (must inherit from Component).</typeparam>
    /// <param name="shellContent">The MauiReactor ShellContent wrapper.</param>
    /// <param name="renderContent">The factory that creates the VisualNode for the page content.</param>
    /// <returns>The configured ShellContent wrapper for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// ShellContent()
    ///     .Title("Home")
    ///     .RenderContent<HomePage>(() => new HomePage())  // Self-contained - no .Set() needed
    /// </code>
    /// </example>
    public static MauiReactor.ShellContent RenderContent<TPage>(
        this MauiReactor.ShellContent shellContent,
        Func<VisualNode> renderContent)
        where TPage : Component
    {
        ArgumentNullException.ThrowIfNull(shellContent);
        ArgumentNullException.ThrowIfNull(renderContent);

        // Get the segment name for the page type (e.g., "HomePage" for class HomePage)
        var segmentName = NavigationSegmentAttribute.GetSegmentName(typeof(TPage));

        // Set Route by modifying the internal MauiReactor ShellContent wrapper's _route field
        // This ensures Route is ready before ShellOnStructureChanged builds the _contentsBySegmentName dictionary
        var shellContentWrapperType = shellContent.GetType();
        var routeField = shellContentWrapperType.GetField("_route",
            BindingFlags.Instance | BindingFlags.NonPublic);
        routeField?.SetValue(shellContent, segmentName);

        // Call MauiReactor's Set with Navigation.PageTypeProperty to trigger NaluShell's PageTypePropertyChanged
        // This will handle setting up DataTemplate, service scope, navigation context, and lifecycle hooks
        shellContent.Set(Navigation.PageTypeProperty, typeof(TPage));

        // Call MauiReactor's built-in RenderContent with user's factory
        // The PageTypePropertyChanged handler has already set up DataTemplate with NaluShell's service scope
        return shellContent.RenderContent(renderContent);
    }
}