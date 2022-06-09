using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace MongoDB.Driver;

public static class AsyncCursorExtensions
{
    public static async IAsyncEnumerable<TDocument> ToAsyncEnumerable<TDocument>(
        this IAsyncCursor<TDocument> cursor,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (cursor == null)
        {
            throw new ArgumentNullException(nameof(cursor));
        }

        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var document in cursor.Current)
            {
                yield return document;
            }
        }
    }

    public static async IAsyncEnumerable<TDocument> ToAsyncEnumerable<TDocument>(
        this IAsyncCursorSource<TDocument> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        using var cursor = await source.ToCursorAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var document in ToAsyncEnumerable(cursor, cancellationToken))
        {
            yield return document;
        }
    }
}