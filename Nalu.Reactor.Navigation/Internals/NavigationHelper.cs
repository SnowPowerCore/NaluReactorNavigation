using System.Reflection;

namespace Nalu;

internal static class NavigationHelper
{
    public static ValueTask SendEnteringAsync(IShellProxy shell, MauiReactor.Component pageComponent, INavigationConfiguration configuration)
    {
        var context = PageNavigationContext.Get(pageComponent);

        if (context.Entered)
        {
            return ValueTask.CompletedTask;
        }

        context.Entered = true;

        if (pageComponent is IEnteringAware enteringAware)
        {
#if DEBUG
            Console.WriteLine($"Entering {pageComponent.GetType().FullName}");
#endif
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Entering, pageComponent, NavigationLifecycleHandling.Handled));

            return enteringAware.OnEnteringAsync();
        }

        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Entering, pageComponent, NavigationLifecycleHandling.NotHandled));

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendLeavingAsync(IShellProxy shell, MauiReactor.Component pageComponent)
    {
        var context = PageNavigationContext.Get(pageComponent);

        if (!context.Entered)
        {
            return ValueTask.CompletedTask;
        }

        context.Entered = false;

        if (pageComponent is ILeavingAware enteringAware)
        {
#if DEBUG
            Console.WriteLine($"Leaving {pageComponent.GetType().FullName}");
#endif
            // ReSharper disable once RedundantArgumentDefaultValue
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Leaving, pageComponent, NavigationLifecycleHandling.Handled));

            return enteringAware.OnLeavingAsync();
        }

        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Leaving, pageComponent, NavigationLifecycleHandling.NotHandled));

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendAppearingAsync(IShellProxy shell, MauiReactor.Component pageComponent, INavigationConfiguration configuration, Action<object> propsDelOnAppearing = null)
    {
        var context = PageNavigationContext.Get(pageComponent);

        if (context.Appeared)
        {
            return ValueTask.CompletedTask;
        }

        context.Appeared = true;

        if (propsDelOnAppearing is not default(Action<object>))
        {
            if (pageComponent.GetType().GetProperty("Props", BindingFlags.Public | BindingFlags.Instance) is PropertyInfo propsProp)
            {
#if DEBUG
                Console.WriteLine($"Appearing {pageComponent.GetType().FullName} with properties init delegate");
#endif
                shell.SendNavigationLifecycleEvent(
                    new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Appearing, pageComponent, NavigationLifecycleHandling.HandledWithIntent, propsDelOnAppearing)
                );
                propsDelOnAppearing.Invoke(propsProp.GetValue(pageComponent));
            }
        }

        if (pageComponent is IAppearingAware appearingAware)
        {
#if DEBUG
            Console.WriteLine($"Appearing {pageComponent.GetType().FullName}");
#endif
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Appearing, pageComponent, NavigationLifecycleHandling.Handled));

            return appearingAware.OnAppearingAsync();
        }

        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Appearing, pageComponent, NavigationLifecycleHandling.NotHandled));

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendDisappearingAsync(IShellProxy shell, MauiReactor.Component pageComponent)
    {
        var context = PageNavigationContext.Get(pageComponent);

        if (!context.Appeared)
        {
            return ValueTask.CompletedTask;
        }

        context.Appeared = false;

        if (pageComponent is IDisappearingAware enteringAware)
        {
#if DEBUG
            Console.WriteLine($"Disappearing {pageComponent.GetType().FullName}");
#endif
            // ReSharper disable once RedundantArgumentDefaultValue
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Disappearing, pageComponent, NavigationLifecycleHandling.Handled));

            return enteringAware.OnDisappearingAsync();
        }

        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Disappearing, pageComponent, NavigationLifecycleHandling.NotHandled));

        return ValueTask.CompletedTask;
    }

    public static ValueTask<bool> CanLeaveAsync(IShellProxy shell, MauiReactor.Component pageComponent)
    {
        if (pageComponent is ILeavingGuard leavingGuard)
        {
#if DEBUG
            Console.WriteLine($"Can leave {pageComponent.GetType().FullName}");
#endif
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.LeavingGuard, pageComponent));

            return leavingGuard.CanLeaveAsync();
        }

        return ValueTask.FromResult(true);
    }

    public static string GetSegmentName(INavigationSegment segment) =>
        segment.SegmentName ?? NavigationSegmentAttribute.GetSegmentName(GetPageType(segment.Type));

    public static Type GetPageType(Type? segmentType)
    {
        ArgumentNullException.ThrowIfNull(segmentType, nameof(segmentType));

        if (segmentType.IsSubclassOf(typeof(MauiReactor.Component)))
        {
            return segmentType;
        }

        throw new InvalidOperationException($"Cannot find page type for segment type {segmentType.FullName}.");
    }
}