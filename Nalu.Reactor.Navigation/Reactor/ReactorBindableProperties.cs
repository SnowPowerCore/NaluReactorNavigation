namespace Nalu.Reactor;

public static class ReactorBindableProperties
{
    public static readonly BindableProperty PageComponentReferenceProperty = BindableProperty.CreateAttached(
        "PageComponentReference",
        typeof(WeakReference<MauiReactor.Component>),
        typeof(ReactorBindableProperties),
        default(WeakReference<MauiReactor.Component>)
    );
}