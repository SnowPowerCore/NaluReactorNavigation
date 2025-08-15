namespace Nalu;

internal interface IShellContentProxy
{
    string SegmentName { get; }
    IShellSectionProxy Parent { get; }
    ShellContent Content { get; }
    Page? Page { get; }
    (MauiReactor.Component, Page) GetOrCreateContent();
    void DestroyContent();
}