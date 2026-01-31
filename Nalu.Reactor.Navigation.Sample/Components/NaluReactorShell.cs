using Nalu;

namespace TestReactorApp.Components;

[Scaffold(typeof(NaluShell))]
public partial class DefaultNaluShell { }

public partial class NaluReactorShell : Component
{
    private IEnumerable<VisualNode> _children = [];
    private Nalu.NaluShell _nativeControl;

    public NaluReactorShell(params IEnumerable<VisualNode?>? children)
    {
        _children = children;
    }

    public override VisualNode Render() =>
        new DefaultNaluShell(
            (c) =>
            {
                _nativeControl = c;
                _nativeControl.Initialize(
                    Services.GetRequiredService<INavigationService>(),
                    "HomePage");
            },
            _children
        );

    protected override void OnWillUnmount()
    {
        base.OnWillUnmount();
        _nativeControl?.Dispose();
        _nativeControl = default;
        _children = default;
    }
}