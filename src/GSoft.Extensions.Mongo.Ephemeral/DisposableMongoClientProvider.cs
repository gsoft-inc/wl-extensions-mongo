using MongoDB.Driver;

namespace GSoft.Extensions.Mongo.Ephemeral;

internal sealed class DisposableMongoClientProvider : IMongoClientProvider, IDisposable
{
    private readonly IMongoClientProvider _underlyingMongoClientProvider;
    private readonly DefaultDatabaseNameHolder _defaultDatabaseNameHolder;

    public DisposableMongoClientProvider(IMongoClientProvider underlyingMongoClientProvider, DefaultDatabaseNameHolder defaultDatabaseNameHolder)
    {
        this._underlyingMongoClientProvider = underlyingMongoClientProvider;
        this._defaultDatabaseNameHolder = defaultDatabaseNameHolder;
    }

    public IMongoClient GetClient(string clientName)
    {
        var underlyingMongoClient = this._underlyingMongoClientProvider.GetClient(clientName);
        return new DisposableMongoClient(underlyingMongoClient, this._defaultDatabaseNameHolder);
    }

    public void Dispose()
    {
        (this._underlyingMongoClientProvider as IDisposable)?.Dispose();
    }
}