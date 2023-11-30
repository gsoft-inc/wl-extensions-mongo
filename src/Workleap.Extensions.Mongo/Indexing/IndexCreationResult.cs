namespace Workleap.Extensions.Mongo.Indexing;

internal class IndexCreationResult
{
    public IList<UniqueIndexName> ExpectedIndexes { get; } = new List<UniqueIndexName>();
}