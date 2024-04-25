using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Ephemeral;

public static class EphemeralMongoBuilderExtensions
{
    /// <summary>
    /// Provides a real implementation of a MongoDB cluster using an ephemeral localhost server that will be destroyed
    /// when it will no longer be used by any running test.
    /// https://github.com/asimmon/ephemeral-mongo
    /// </summary>
    public static MongoBuilder UseEphemeralRealServer(this MongoBuilder builder)
    {
        builder.Services.AddSingleton<DefaultDatabaseNameHolder>();
        builder.Services.AddSingleton<ReusableMongoRunnerProvider>();
        builder.Services.ConfigureOptions<ConfigureEphemeralMongoClientOptions>();

        builder.Services.DecorateWithSameLifetime<IMongoClientProvider>(static (serviceProvider, underlyingMongoClientProvider) =>
        {
            var defaultDatabaseNameHolder = serviceProvider.GetRequiredService<DefaultDatabaseNameHolder>();
            return new DisposableMongoClientProvider(underlyingMongoClientProvider, defaultDatabaseNameHolder);
        });

        return builder;
    }

    public static MongoBuilder UseEphemeralRealServer(this MongoBuilder builder, string connectionString)
    {
        var client = new MongoClient(connectionString);
        return UseEphemeralRealServer(builder, client);
    }

    public static MongoBuilder UseEphemeralRealServer(this MongoBuilder builder, IMongoClient client)
    {
        builder.Services.AddSingleton<DefaultDatabaseNameHolder>();
        builder.Services.AddSingleton<IMongoClientProvider>(serviceProvider =>
        {
            var defaultDatabaseNameHolder = serviceProvider.GetRequiredService<DefaultDatabaseNameHolder>();
            return new DisposableMongoClientProvider(new SingletonMongoClientProvider(client), defaultDatabaseNameHolder);
        });

        return builder;
    }
}