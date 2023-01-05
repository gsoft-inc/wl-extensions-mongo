using MongoDB.Driver;

namespace GSoft.Extensions.Mongo;

public interface IMongoClientProvider
{
    IMongoClient GetClient(string clientName);
}