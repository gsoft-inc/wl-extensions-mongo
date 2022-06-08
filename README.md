# GSoft.Infra.Mongo

Provides MongoDB access through **.NET dependency injection**, following `Microsoft.Extensions.*` library practices.

Features:
* **Automatic indexes** creation, update and removal based on code changes,
* **Encryption at the property level** with different scopes (per user, tenant, or application-wide),
* `IServiceCollection` enabled using `IServiceCollection` and `IServiceProvider`,
* **Highly configurable** (custom serializers, conventions, multiple databases support)
* `IAsyncEnumerable` support


## Getting started

Install the package `GSoft.Infra.Mongo.Abstractions` in the project where you'll declare your documents.
This package contains base classes and interfaces such as `MongoDocument`, `MongoIndexProvider`, `MongoCollectionAttribute`.
There's also a few extension methods on MongoDB C# driver classes and interfaces that adds `IAsyncEnumerable` support to cursors.

Install the package `GSoft.Infra.Mongo` at the application entry point level to register and configure the dependencies in a `IServiceCollection`.


## Example

```csharp
// In the project that contains the documents:
// 1) Declare the collection name (camelCase) and the type responsible for providing indexes (optional)
[MongoCollection("people", IndexProviderType = typeof(PersonDocumentIndexes))]
public class PersonDocument : MongoDocument
{
    public string Name { get; set; } = string.Empty;
}

public class PersonDocumentIndexes : MongoIndexProvider<PersonDocument>
{
    public override IEnumerable<CreateIndexModel<PersonDocument>> CreateIndexModels()
    {
        // Index name is mandatory
        yield return new CreateIndexModel<PersonDocument>(
            Builders<PersonDocument>.IndexKeys.Combine().Ascending(x => x.Name),
            new CreateIndexOptions { Name = "name" });
    }
}

// 2) In the project that configures the application:
var services = new ServiceCollection();
services.AddMongo(ConfigureMongo).AddEncryptor<YourMongoValueEncryptor>();

private static void ConfigureMongo(MongoOptions options)
{
    // Simplified for demo purposes, it is better to use appsettings.json, secret vaults
    // and IConfigureOptions<> classes that can use dependency injection to access other options or dependencies
    options.ConnectionString = "mongodb://localhost";
    options.DefaultDatabaseName = "default";
    
    // There are built-in serializers and conventions registered, but you can remove or override them
    // ⚠ Careful, these are objects that will live for the entire lifetime of the application (singleton) as MongoDB C# driver
    // uses static properties to configure its behavior and serialization
    options.BsonSerializers[typeof(Foo)] = new MyFooBsonSerializer();
    options.ConventionPacks.Add(new MyConventionPack());
    options.EnableSensitiveInformationLogging = true;

    // Used by the automatic index update feature
    options.Indexing.LockMaxLifetimeInSeconds = 300;
    options.Indexing.LockAcquisitionTimeoutInSeconds = 60;
    options.Indexing.ApplicationVersion = new Version(1, 0, 0);
}

// MongoDB document properties can be encrypted when decorated with the [SensitiveInformation(scope)] attribute
// There is a convention pack that use this class to encrypt and decrypt values using a custom BsonSerializer.
// This is not required if you never use the attribute.
private sealed class YourMongoValueEncryptor : IMongoValueEncryptor
{
    public byte[] Encrypt(byte[] bytes, SensitivityScope sensitivityScope)
    {
        // Encrypt the bytes based on the sensitivity scope
        return bytes;
    }

    public byte[] Decrypt(byte[] bytes, SensitivityScope sensitivityScope)
    {
        // Decrypt the bytes based on the sensitivity scope
        return bytes;
    }
}

// 3) Consume the registered services
// Automatically update indexes if their definition in the code has changed - a cryptographic hash is used to detect changes.
// There's a distributed lock that prevents race conditions, and the application version is used to deal with deployements rollbacks.
var indexer = this.Services.GetRequiredService<IMongoIndexer>();
await indexer.UpdateIndexesAsync(new[] { typeof(PersonDocument) });
await indexer.UpdateIndexesAsync(new[] { typeof(PersonDocument).Assembly }); // Assembly scanning alternative

// No need to know the collection name, just use the document type
// No cursors handling needed, use IAsyncEnumerable
var collection = this.Services.GetRequiredService<IMongoDatabase>().GetCollection<PersonDocument>();
var people = await collection.Find(FilterDefinition<PersonDocument>.Empty).ToAsyncEnumerable().ToListAsync();
```