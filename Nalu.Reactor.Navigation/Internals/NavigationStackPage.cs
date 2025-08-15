namespace Nalu;

internal record NavigationStackPage(string Route, string SegmentName, MauiReactor.Component PageComponent, bool IsModal);