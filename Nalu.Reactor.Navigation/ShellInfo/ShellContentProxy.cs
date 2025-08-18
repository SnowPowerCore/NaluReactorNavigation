using System.Reflection;
using Nalu.Reactor;

namespace Nalu;

#pragma warning disable CS8618

internal class ShellContentProxy : IShellContentProxy
{
    private static readonly PropertyInfo _shellContentCacheProperty = typeof(ShellContent).GetProperty("ContentCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private readonly ShellContent _content;
    private readonly IShellSectionProxy _parent;

    public ShellContentProxy(ShellContent content, IShellSectionProxy parent)
    {
        _parent = parent;
        _content = content;
    }

    public ShellContent Content => _content;

    public string SegmentName => _content.Route;

    public IShellSectionProxy Parent => _parent;
    
    public Page? Page => ((IShellContentController)_content).Page;

    public (MauiReactor.Component, Page) GetOrCreateContent()
    {
        var page = ((IShellContentController)_content).GetOrCreateContent();
        var pageComponentWeakRef = (WeakReference<MauiReactor.Component>)page.GetValue(ReactorBindableProperties.PageComponentInstanceProperty);
        if (!pageComponentWeakRef.TryGetTarget(out var pageComponent))
            return (default, page);
        return (pageComponent, page);
    }

    public void DestroyContent()
    {
        _content?.SetValue(ReactorBindableProperties.PageComponentInstanceProperty, default);

        if (Page is not { } page)
        {
            return;
        }

        var pageComponentWeakRef = (WeakReference<MauiReactor.Component>)page.GetValue(ReactorBindableProperties.PageComponentInstanceProperty);
        if (pageComponentWeakRef.TryGetTarget(out var pageComponent))
        {
            PageNavigationContext.Dispose(pageComponent);
            var navContextProp = (PageNavigationContext)page.GetValue(PageNavigationContext.NavigationContextProperty);
            navContextProp?.Dispose();
            page.SetValue(PageNavigationContext.NavigationContextProperty, default);
            page.SetValue(ReactorBindableProperties.PageComponentInstanceProperty, default);
        }
        _shellContentCacheProperty.SetValue(_content, default);
    }
}