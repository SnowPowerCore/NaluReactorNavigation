using System.Reflection;
using MauiReactor;

namespace Nalu;

public class ShellNaluReactorComponentRouteFactory : RouteFactory
{
    private readonly Queue<(Component component, Action<object> propsInit)> _pages = new();

    public void Push(Component page, Action<object> propsInit) =>
        _pages.Enqueue((page, propsInit));

    public override Element GetOrCreate()
    {
        if (_pages.TryDequeue(out ValueTuple<Component, Action<object>> item))
        {
            var component = item.Item1;
            var propsInit = item.Item2;
            var pageComponent = TemplateHost.Create(component);
            if (propsInit is not default(Action<object>))
            {
                if (component.GetType().GetProperty("Props", BindingFlags.Public | BindingFlags.Instance) is PropertyInfo propsProp)
                {
                    propsInit.Invoke(propsProp.GetValue(component));
                }
            }
            var page = pageComponent.NativeElement;
            page.SetValue(
                Reactor.ReactorBindableProperties.PageComponentReferenceProperty,
                new WeakReference<Component>(component));
            if (page is Element elementPage)
            {
                elementPage.HandlerChanged += OnHandlerChanged;

                void OnHandlerChanged(object? sender, EventArgs e)
                {
                    if (elementPage is not default(Element)
                        && (!elementPage.Handler?.IsConnected() ?? true))
                    {
                        elementPage.HandlerChanged -= OnHandlerChanged;
                        elementPage.SetValue(
                            Reactor.ReactorBindableProperties.PageComponentReferenceProperty,
                            default);
                        (pageComponent as IHostElement)?.Stop();
                    }
                };
            }
            return (Element)page;
        }
        return default;
    }

    public override Element GetOrCreate(IServiceProvider services) => GetOrCreate();
}