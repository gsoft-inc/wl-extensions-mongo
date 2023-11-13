namespace Workleap.Extensions.Mongo.Indexing;

internal class IndexProcessingResult
{
    public string CollectionName { get; set; } = string.Empty;

    public IList<UniqueIndexName> ExpectedIndexes { get; } = new List<UniqueIndexName>();
}