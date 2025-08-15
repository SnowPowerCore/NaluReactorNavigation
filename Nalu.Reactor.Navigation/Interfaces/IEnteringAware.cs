namespace Nalu;

/// <summary>
/// <see cref="INavigationService" /> will invoke <see cref="OnEnteringAsync" /> method when the page is being
/// pushed onto the navigation stack and no navigation intent has been provided.
/// </summary>
/// <remarks>
/// It's not recommended to implement long operations in this method.
/// The user perceives the navigation as slow if the page takes a long time to appear.
/// </remarks>
public interface IEnteringAware
{
    /// <summary>
    /// Invoked when the page is appearing without a navigation intent.
    /// </summary>
    ValueTask OnEnteringAsync();
}