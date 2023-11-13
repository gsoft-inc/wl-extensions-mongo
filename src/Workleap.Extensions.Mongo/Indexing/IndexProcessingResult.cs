namespace Workleap.Extensions.Mongo.Indexing;

internal class IndexProcessingResult
{
    public string CollectionName { get; set; }

    public IList<UniqueIndexName> ExpectedIndexes { get; } = new List<UniqueIndexName>();
}