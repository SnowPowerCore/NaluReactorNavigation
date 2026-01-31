using Page = Microsoft.Maui.Controls.Page;

namespace Nalu;

internal static class InternalNavigationExtensions
{
    internal static HashSet<Page> GetNavigationStack(this INavigation navigation) =>
        navigation.NavigationStack
            .Concat(navigation.ModalStack)
            .Where(p => p is not default(Page))
            .ToHashSet();
}