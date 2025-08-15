namespace Nalu;

internal class ShellRouteFactory
{
    private readonly Dictionary<Type, ShellNaluReactorComponentRouteFactory> _factories = [];

    public ShellNaluReactorComponentRouteFactory GetRouteFactory(Type pageType)
    {
        if (!_factories.TryGetValue(pageType, out var factory))
        {
            factory = new ShellNaluReactorComponentRouteFactory();
            _factories[pageType] = factory;
        }

        return factory;
    }
}