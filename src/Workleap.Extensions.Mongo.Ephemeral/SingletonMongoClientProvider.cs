using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Ephemeral;

internal sealed class SingletonMongoClientProvider : IMongoClientProvider
{
    private readonly IMongoClient _client;

    public SingletonMongoClientProvider(IMongoClient client)
    {
        this._client = client;
    }

    public IMongoClient GetClient(string clientName)
    {
        return this._client;
    }
}