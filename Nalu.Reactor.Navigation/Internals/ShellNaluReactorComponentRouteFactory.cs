using System.Reflection;

namespace Nalu;

public class ShellNaluReactorComponentRouteFactory : RouteFactory
{
    private readonly Queue<(MauiReactor.Component component, Action<object> propsInit)> _pages = new();

    public void Push(MauiReactor.Component page, Action<object> propsInit) =>
        _pages.Enqueue((page, propsInit));

    public override Element GetOrCreate()
    {
        if (_pages.TryDequeue(out ValueTuple<MauiReactor.Component, Action<object>> item))
        {
            var component = item.Item1;
            var propsInit = item.Item2;
            var pageComponent = MauiReactor.TemplateHost.Create(component);
            if (propsInit is not default(Action<object>))
            {
                if (component.GetType().GetProperty("Props", BindingFlags.Public | BindingFlags.Instance) is PropertyInfo propsProp)
                {
                    propsInit.Invoke(propsProp.GetValue(component));
                }
            }
            var page = pageComponent.NativeElement;
            page.SetValue(
                Reactor.ReactorBindableProperties.PageComponentInstanceProperty,
                component);
            return (Element)page;
        }
        return default;
    }

    public override Element GetOrCreate(IServiceProvider services) => GetOrCreate();
}