using MauiReactor;
using Nalu.Models;

namespace Nalu.Reactor;

[Scaffold(typeof(NaluShell))]
public partial class DefaultNaluShell { }

public partial class NaluReactorShell : Component
{
    private readonly IEnumerable<VisualNode> _children = [];
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
                    Services.GetRequiredService<RootTypeModel>().Type.Name);
            },
            _children
        );

    protected override void OnWillUnmount()
    {
        base.OnWillUnmount();
        _nativeControl?.Dispose();
    }
}