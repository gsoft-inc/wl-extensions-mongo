# ShareGate.Infra.Mongo

Provides MongoDB access through **.NET dependency injection**, following `Microsoft.Extensions.*` library practices.

Features:
* **Automatic indexes** creation, update and removal based on code changes,
* **Encryption at the property level** with different scopes (per user, tenant, or application-wide),
* **Dependency-injection** enabled using `IServiceCollection` and `IServiceProvider`,
* **Highly configurable** (custom serializers, conventions, multiple databases support)
* `IAsyncEnumerable` support


## Getting started

Install the package `ShareGate.Infra.Mongo.Abstractions` in the project where you'll declare your documents.
This package contains base classes and interfaces such as `IMongoDocument`, `MongoIndexProvider`, `MongoCollectionAttribute`.
There's also a few extension methods of the MongoDB C# driver classes and interfaces that adds `IAsyncEnumerable` support to cursors.

Install the package `ShareGate.Infra.Mongo` at the application entry point level to register and configure the dependencies in a `IServiceCollection`.

Install the package `ShareGate.Infra.Mongo.Ephemeral` whenever you want to use a real but ephemeral MongoDB cluster with a single node replica set.
This is ideal for integration testing, as each `IServiceProvider` will have access to an unique and isolated database.


## Example

```csharp
// In the project that contains the documents:
// 1) Declare the collection name (camelCase) and the type responsible for providing indexes (optional)
[MongoCollection("people", IndexProviderType = typeof(PersonDocumentIndexes))]
public class PersonDocument : MongoDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

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
```

```csharp
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
}

// MongoDB document properties can be encrypted when decorated with the [SensitiveInformation(scope)] attribute
// There is a convention pack that use this class to encrypt and decrypt values using a custom BsonSerializer.
// This is not required if you never use the attribute.
private sealed class YourMongoValueEncryptor : IMongoValueEncryptor
{
    // Encrypt and decrypt the bytes based on the sensitivity scope
    // Use AsyncLocal<> to determine if the sensitivity scopes matches the current execution context.
    // For instance, SensitivityScope.User should only work if there is actually an authenticated user detected through IHttpContextAccessor,
    // or any other ambient mechanism that relies on AsyncLocal<>.
    public byte[] Encrypt(byte[] bytes, SensitivityScope sensitivityScope) => bytes;
    public byte[] Decrypt(byte[] bytes, SensitivityScope sensitivityScope) => bytes;
}
```

```csharp
// 3) Consume the registered services
// Automatically update indexes if their definition in the code has changed - a cryptographic hash is used to detect changes.
// There's a distributed lock that prevents race conditions.
var indexer = this.Services.GetRequiredService<IMongoIndexer>();
await indexer.UpdateIndexesAsync(new[] { typeof(PersonDocument) });
await indexer.UpdateIndexesAsync(new[] { typeof(PersonDocument).Assembly }); // Assembly scanning alternative

// No need to know the collection name, just use the document type
// No cursors handling needed, use IAsyncEnumerable
var collection = this.Services.GetRequiredService<IMongoDatabase>().GetCollection<PersonDocument>();
var people = await collection.Find(FilterDefinition<PersonDocument>.Empty).ToAsyncEnumerable().ToListAsync();
```

```csharp
// 4) Add the ShareGate.Infra.Mongo.Ephemeral package to use a ephemeral but real MongoDB database in your tests
var services = new ServiceCollection();
services.AddMongo().UseEphemeralRealServer();
```