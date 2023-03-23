using System.Collections.Immutable;

namespace GSoft.Extensions.Mongo.Analyzers.Internals;

internal static class ImmutableHashSetExtensions
{
    // See: https://github.com/dotnet/roslyn-analyzers/blob/v3.3.4/src/Utilities/Compiler/Extensions/ImmutableHashSetExtensions.cs#L91
    public static void AddIfNotNull<T>(this ImmutableHashSet<T>.Builder builder, T? item)
        where T : class
    {
        if (item != null)
        {
            builder.Add(item);
        }
    }
}