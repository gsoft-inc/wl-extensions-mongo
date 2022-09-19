using MongoDB.Driver;

namespace ShareGate.Infra.Mongo;

public interface IMongoClientProvider
{
    IMongoClient GetClient(string clientName);
}