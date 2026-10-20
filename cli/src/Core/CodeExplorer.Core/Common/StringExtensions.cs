using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CodeExplorer.Core.Common;

public static class StringExtensions
{
    /// <summary>
    /// Case-insensitive StartsWith using StringComparison.OrdinalIgnoreCase.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartWithIC([NotNullWhen(true)] this string? str, string prefix) =>
        str != null && str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Case-insensitive StartsWith using StringComparison.OrdinalIgnoreCase.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartsWithIC([NotNullWhen(true)] this string? str, string prefix) =>
        str != null && str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
