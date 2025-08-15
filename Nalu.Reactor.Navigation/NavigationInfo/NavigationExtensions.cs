using System.Collections;
using System.Reflection;

namespace Nalu;

/// <summary>
/// <see cref="INavigationInfo" /> extension methods.
/// </summary>
public static class NavigationExtensions
{
    /// <summary>
    /// Compares two <see cref="Navigation" />s for equality.
    /// </summary>
    /// <param name="self">The navigation to compare.</param>
    /// <param name="other">The other navigation object.</param>
    public static bool Matches(this INavigationInfo self, INavigationInfo? other)
    {
        ArgumentNullException.ThrowIfNull(self);

        return Matches(self, other, GetIntentComparer(self.PropsDelegate ?? other?.PropsDelegate));
    }

    /// <summary>
    /// Compares two <see cref="Navigation" />s for equality.
    /// </summary>
    /// <param name="self">The navigation to compare.</param>
    /// <param name="other">The other navigation object.</param>
    /// <param name="intentComparer">An equality comparer for intents.</param>
    public static bool Matches(this INavigationInfo self, INavigationInfo? other, IEqualityComparer? intentComparer)
    {
        ArgumentNullException.ThrowIfNull(self);

        if (other is default(INavigationInfo) || other.Path != self.Path)
        {
            return false;
        }

        if (other.PropsDelegate is default(Action<object>) && self.PropsDelegate is default(Action<object>))
        {
            return true;
        }

        if (other.PropsDelegate is default(Action<object>) || self.PropsDelegate is default(Action<object>))
        {
            return false;
        }

        if (other.PropsDelegate.GetType() != self.PropsDelegate.GetType())
        {
            return false;
        }

        return (intentComparer ?? EqualityComparer<object>.Default).Equals(self.PropsDelegate, other.PropsDelegate);
    }

    /// <summary>
    /// Compares two <see cref="Navigation" />s for equality.
    /// </summary>
    /// <typeparam name="TIntent">Expected type for intents.</typeparam>
    /// <param name="self">The navigation to compare.</param>
    /// <param name="other">The other navigation object.</param>
    /// <param name="intentComparer">An function to check intent equality.</param>
    public static bool Matches<TIntent>(this INavigationInfo self, INavigationInfo? other, Func<TIntent, TIntent, bool> intentComparer)
    {
        ArgumentNullException.ThrowIfNull(self);

        return other is not default(INavigationInfo) &&
               other.Path == self.Path &&
               ((self.PropsDelegate is default(Action<object>) && other.PropsDelegate is default(Action<object>)) || (self.PropsDelegate is TIntent intent &&
                                                                  other.PropsDelegate is TIntent otherIntent &&
                                                                  intentComparer(intent, otherIntent)));
    }

    private static IEqualityComparer GetIntentComparer(object? intent)
    {
        if (intent is default(object))
        {
            return EqualityComparer<object>.Default;
        }

        var type = intent.GetType();
        var equalityComparerType = typeof(EqualityComparer<>).MakeGenericType(type);
        var defaultProperty = equalityComparerType.GetProperty(nameof(EqualityComparer<object>.Default), BindingFlags.Public | BindingFlags.Static);

        return (IEqualityComparer) defaultProperty?.GetValue(default)!;
    }
}