using MongoDB.Driver;

namespace Workleap.Extensions.Mongo;

public interface IMongoClientProvider
{
    IMongoClient GetClient(string clientName);
}