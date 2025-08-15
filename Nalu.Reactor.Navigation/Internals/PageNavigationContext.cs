using System.Runtime.CompilerServices;

namespace Nalu;

internal sealed partial class PageNavigationContext : IDisposable
{
    private IServiceScope? _serviceScope;

    public PageNavigationContext(IServiceScope serviceScope)
    {
        _serviceScope = serviceScope;
    }

    public IServiceScope ServiceScope => _serviceScope ?? throw new ObjectDisposedException(nameof(PageNavigationContext));

    public bool Entered { get; set; }

    public bool Appeared { get; set; }

    public static readonly BindableProperty NavigationContextProperty = BindableProperty.CreateAttached(
        "PageNavigationContext",
        typeof(PageNavigationContext),
        typeof(PageNavigationContext),
        null
    );

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_attachedProperties")]
    private extern static ref Dictionary<BindableProperty, object?> GetAttachedPropertiesField(MauiReactor.Component @this);

    public static PageNavigationContext Get(MauiReactor.Component page)
    {
        var pageNavigationContext = (PageNavigationContext)GetAttachedPropertiesField(page).GetValueOrDefault(NavigationContextProperty);
#pragma warning disable IDE0270
        if (pageNavigationContext is null)
#pragma warning restore IDE0270
        {
            throw new InvalidOperationException("Cannot navigate to a page not created by Nalu navigation.");
        }

        return pageNavigationContext;
    }

    public static bool HasNavigationContext(MauiReactor.Component page) => page.HasPropertySet(NavigationContextProperty);

    public static void Set(MauiReactor.Component page, PageNavigationContext? context) =>
        page.SetProperty(NavigationContextProperty, context);

    public static void Dispose(MauiReactor.Component page)
    {
        var context = Get(page);
        context.Dispose();
        Set(page, null);
    }

    public void Dispose()
    {
        _serviceScope?.Dispose();
        _serviceScope = null;
    }
}