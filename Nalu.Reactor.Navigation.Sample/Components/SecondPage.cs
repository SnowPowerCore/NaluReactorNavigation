using Nalu;

namespace TestReactorApp.Components;

public partial class SecondPage : Component
{
    [Inject]
    private readonly INavigationService _navigation;

    private bool _disposed = false;

    public override VisualNode Render() =>
        ContentPage(
            VStack(
                Button("Go to First Page")
                    .OnClicked(NavigateToFirstPageAsync)
                    .HCenter()
            )
            .VCenter()
        )
        .BackgroundColor(Colors.Red)
        .Title("Second page");

    private Task NavigateToFirstPageAsync() =>
        _navigation.GoToAsync(Nalu.Navigation.Relative().Pop());

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if(!_disposed)
        {
            _disposed = true;
        }
    }
}