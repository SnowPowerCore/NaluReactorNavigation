using System.Text.RegularExpressions;
using Nalu.Internals;
// ReSharper disable once RedundantUsingDirective
using System.Windows.Input;

namespace Nalu;

#pragma warning disable IDE0290
#pragma warning disable VSTHRD100

/// <summary>
/// Nalu shell, the shell navigation you wanted.
/// </summary>
public partial class NaluShell : Shell, INaluShell, IDisposable
{
    private string RootPageRoute { get; set; }
    private bool _initialized;
    private ShellProxy? _shellProxy;

    /// <summary>
    /// Occurs when a navigation event is triggered.
    /// </summary>
    public event EventHandler<NavigationLifecycleEventArgs>? NavigationEvent;

    internal NavigationService NavigationService { get; private set; }

    internal IServiceProvider ServiceProvider { get; private set; }

    IShellProxy INaluShell.ShellProxy => _shellProxy ?? throw new InvalidOperationException("The shell info is not available yet.");

    public void Initialize(INavigationService navigationService, string rootPageRoute)
    {
        if (_initialized)
        {
            return;
        }

        NavigationService = (NavigationService)navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        RootPageRoute = rootPageRoute ?? throw new ArgumentNullException(nameof(rootPageRoute));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent is not default(Element) && !_initialized)
        {
            _shellProxy = new ShellProxy(this, NavigationService.ServiceProvider);
            _shellProxy.InitializeWithContent(RootPageRoute);
            _ = NavigationService.InitializeAsync(_shellProxy, RootPageRoute);
            _initialized = true;
        }
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="NaluShell" />.
    /// </summary>
    /// <param name="disposing">True when disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shellProxy?.Dispose();
        }
    }

    /// <inheritdoc />
    protected override bool OnBackButtonPressed()
    {
#if WINDOWS || !(IOS || ANDROID || MACCATALYST)
        var backButtonBehavior = GetBackButtonBehavior(GetVisiblePage());

        if (backButtonBehavior is not default(BackButtonBehavior))
        {
            var command = backButtonBehavior.GetPropertyIfSet<ICommand>(BackButtonBehavior.CommandProperty, default!);
            var commandParameter = backButtonBehavior.GetPropertyIfSet<object>(BackButtonBehavior.CommandParameterProperty, default!);

            if (command is not default(ICommand))
            {
                command.Execute(commandParameter);

                return true;
            }
        }
#endif

        if (GetVisiblePage() is { } page && page.SendBackButtonPressed())
        {
            return true;
        }

        var currentContent = CurrentItem?.CurrentItem;

        if (currentContent is not default(ShellSection) && currentContent.Stack.Count > 1)
        {
            DispatchNavigation(Nalu.Navigation.Relative().Pop());

            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>
    /// Triggered when a navigation is about to happen.
    /// </summary>
    /// <remarks>
    /// Gives the ability to cancel the navigation.
    /// </remarks>
    /// <param name="args"></param>
    protected virtual void OnNavigating(NaluShellNavigatingEventArgs args) { }

    internal void SendOnNavigating(NaluShellNavigatingEventArgs args) => OnNavigating(args);

    /// <inheritdoc />
    protected sealed override void OnNavigating(ShellNavigatingEventArgs args)
    {
        var uri = args.Target.Location.OriginalString;
        var currentUri = args.Current?.Location.OriginalString ?? string.Empty;
        
        if (!_initialized || // Shell initialization process
            Handler is default(IViewHandler) || // Shell initialization process
            string.IsNullOrEmpty(uri) || // An empty URI is very likely Android trying to background the app when on a root page and back button is pressed.
            CommunityToolkitPopupRegex().IsMatch(uri) || // CommunityToolkit popup navigation
            CommunityToolkitPopupRegex().IsMatch(currentUri) || // CommunityToolkit popup navigation
            IsRemovingStackPages(args) || // ShellSectionProxy removing pages from the stack during cross-item navigation
            uri.EndsWith("?nalu")) // Nalu-triggered navigations
        {
            return;
        }

        args.Cancel();

        if (uri == "..")
        {
            DispatchNavigation(Nalu.Navigation.Relative().Pop());

            return;
        }

        // Only reason we're here is due to shell content navigation from Shell Flyout or Tab bars
        // Now find the ShellContent target and navigate to it via the navigation service
        var segments = uri
                       .Split('/', StringSplitOptions.RemoveEmptyEntries)
                       .Select(NormalizeSegment)
                       .ToArray();

        var shellContent = (ShellContentProxy) _shellProxy!.FindContent(segments);
        var shellSection = shellContent.Parent;

        var ownsNavigationStack = shellSection.CurrentContent == shellContent;
        var navigation = (Navigation) Nalu.Navigation.Absolute();

        navigation.Add(
            new NavigationSegment
            {
                Type = Nalu.Navigation.GetPageType(shellContent.Content),
                SegmentName = shellContent.SegmentName
            }
        );

        if (ownsNavigationStack)
        {
            var navigationStackPages = shellSection.GetNavigationStack(NavigationService.ServiceProvider).ToArray();
            var segmentsCount = segments.Length;
            var navigationStackCount = navigationStackPages.Length;

            for (var i = 1; i < segmentsCount && i < navigationStackCount; i++)
            {
                var stackPage = navigationStackPages[i];

                navigation.Add(
                    new NavigationSegment
                    {
                        Type = stackPage.PageComponent.GetType(),
                        SegmentName = stackPage.SegmentName
                    }
                );
            }
        }

        DispatchNavigation(navigation);
    }

    private bool IsRemovingStackPages(ShellNavigatingEventArgs args)
    {
        if (args.Source is not ShellNavigationSource.Remove)
        {
            return false;
        }

        var segments = args.Target.Location.OriginalString
                           .Split('/', StringSplitOptions.RemoveEmptyEntries)
                           .Select(NormalizeSegment)
                           .ToArray();

        var shellContent = (ShellContentProxy) _shellProxy!.FindContent(segments);
        var shellSection = shellContent.Parent;

        // If the ShellContent relative to a stack page being removed does not have a page,
        // it means this can only be Nalu navigation cleaning up the stack after a cross-item navigation.
        // If that's not null, then check if any of the pages in the stack is marked for removal.
        var isRemovingStackPages = shellContent.Page is default(Page) ||
            shellSection.GetNavigationStack(NavigationService.ServiceProvider, shellContent).Any(stackPage =>
                ShellSectionProxy.IsPageMarkedForRemoval(stackPage.PageComponent.ContainerPage));

        return isRemovingStackPages;
    }

    internal void SendNavigationLifecycleEvent(NavigationLifecycleEventArgs args) => NavigationEvent?.Invoke(this, args);

    private void DispatchNavigation(INavigationInfo navigation) =>
        Dispatcher.Dispatch(() => NavigationService.GoToAsync(navigation).FireAndForget(Handler));

    private Page? GetVisiblePage()
    {
        if (CurrentItem?.CurrentItem is IShellSectionController scc)
        {
            return scc.PresentedPage;
        }

        return default;
    }

    [GeneratedRegex("^(D_FAULT_|IMPL_)")]
    private static partial Regex NormalizeSegmentRegex();

    private static string NormalizeSegment(string segment) =>
        NormalizeSegmentRegex().Replace(segment, string.Empty);

    // See: https://github.com/CommunityToolkit/Maui/blob/main/src/CommunityToolkit.Maui/Extensions/PopupExtensions.shared.cs#L165
    // We need to match: $"{nameof(PopupPage)}" + Guid.NewGuid();
    // In example: "PopupPageca6500ff-c430-49d4-9f79-f5536f71f959";
    [GeneratedRegex(@"\bPopupPage[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.IgnoreCase)]
    private static partial Regex CommunityToolkitPopupRegex();
}