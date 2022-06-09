using ShareGate.Infra.Mongo.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ShareGate.Infra.Mongo;

public sealed class MongoBuilder
{
    public MongoBuilder(IServiceCollection services)
    {
        this.Services = services;
    }

    public IServiceCollection Services { get; }

    public MongoBuilder AddEncryptor<TEncryptor>()
        where TEncryptor : class, IMongoValueEncryptor
    {
        this.Services.AddSingleton<IMongoValueEncryptor, TEncryptor>();
        return this;
    }
}