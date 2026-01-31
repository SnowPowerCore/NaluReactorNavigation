using Nalu;

namespace TestReactorApp.Components;

public class AppShell : Component
{
    private readonly HomePage _homePage = new();

    public override VisualNode Render() =>
        new NaluReactorShell(
            ShellContent()
                .Title("Home")
                .RenderContent<HomePage>(() => _homePage)
        );
}