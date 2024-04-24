namespace Workleap.Extensions.Mongo;

public interface IMongoCollectionConfiguration<TDocument>
    where TDocument : class
{
    void Configure(IMongoCollectionBuilder<TDocument> builder);
}