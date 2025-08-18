namespace Nalu.Reactor;

public static class ReactorBindableProperties
{
    public static readonly BindableProperty PageComponentInstanceProperty = BindableProperty.CreateAttached(
        "PageComponentInstance",
        typeof(WeakReference<MauiReactor.Component>),
        typeof(ReactorBindableProperties),
        default(WeakReference<MauiReactor.Component>)
    );
}