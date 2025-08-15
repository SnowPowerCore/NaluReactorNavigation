namespace Nalu;

/// <summary>
/// <see cref="INavigationService" /> will invoke <see cref="OnAppearingAsync" /> method when the page is appearing
/// and no navigation intent has been provided.
/// </summary>
public interface IAppearingAware
{
    /// <summary>
    /// Invoked when the page is appearing without a navigation intent.
    /// </summary>
    /// <returns>A task which completes when appearing routines are completed.</returns>
    ValueTask OnAppearingAsync();
}