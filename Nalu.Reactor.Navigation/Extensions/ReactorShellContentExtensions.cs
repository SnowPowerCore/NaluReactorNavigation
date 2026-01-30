using System.Runtime.CompilerServices;
using MauiReactor;
using Nalu.Reactor;

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
    ///     .RenderContent<HomePage>(() => new HomePage())
    /// </code>
    /// </example>
    public static MauiReactor.ShellContent RenderContent<TPage>(
        this MauiReactor.ShellContent shellContent,
        Func<VisualNode> renderContent)
        where TPage : Component
    {
        ArgumentNullException.ThrowIfNull(shellContent);
        ArgumentNullException.ThrowIfNull(renderContent);

        // Call MauiReactor's Set with Navigation.PageTypeProperty to trigger NaluShell's PageTypePropertyChanged
        // This will handle setting up DataTemplate, service scope, navigation context, and lifecycle hooks
        shellContent.Set(Navigation.PageTypeProperty, typeof(TPage));

        // Create wrapper that manages component lifecycle during hot-reload
        return shellContent.RenderContent(() =>
        {
#if DEBUG
            var currentlyDisplayedShellContent = Microsoft.Maui.Controls.Shell.Current
                .CurrentItem?.CurrentItem?.CurrentItem;
            var pageComponentWeakRef = (WeakReference<Component>)currentlyDisplayedShellContent
                .GetValue(ReactorBindableProperties.PageComponentReferenceProperty);
            if (pageComponentWeakRef?.TryGetTarget(out var existingComponent) == true)
            {
                Application.Current.Dispatcher
                    .Dispatch(() => InvalidateComponent(existingComponent));
            }
#endif
            return renderContent();
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InvalidateComponent")]
    extern static void InvalidateComponent(Component c);
}