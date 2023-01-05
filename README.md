# GSoft.Extensions.Mongo

[![GSoft.Extensions.Mongo package in SGCloudCopy feed in Azure Artifacts](https://feeds.dev.azure.com/sharegate/_apis/public/Packaging/Feeds/SGCloudCopy/Packages/58873f85-2ee7-45ac-b7ee-1516a44a0a2c/Badge)](https://dev.azure.com/sharegate/GSoft.CloudCopy/_artifacts/feed/SGCloudCopy/NuGet/GSoft.Extensions.Mongo/)
[![Build Status](https://dev.azure.com/sharegate/GSoft.CloudCopy/_apis/build/status/GSoft.Extensions.Mongo/GSoft.Extensions.Mongo%20CI?branchName=main)](https://dev.azure.com/sharegate/GSoft.CloudCopy/_build/latest?definitionId=307&branchName=main)

Provides MongoDB access through **.NET dependency injection**, following `Microsoft.Extensions.*` library practices with several features:

* **Automatic indexes** creation, update and removal based on code changes,
* **Encryption at field level** with different scopes (per user, tenant, or application-wide),
* **Dependency-injection** enabled using `IServiceCollection` and `IServiceProvider`,
* **Highly configurable** (custom serializers, conventions, multiple databases support)
* Support for **multiple MongoDB connection strings** and MongoDB clients
* `IAsyncEnumerable` support,


## Getting started

Install the package `GSoft.Extensions.Mongo.Abstractions` in the project where you'll declare your documents.
This package contains base classes and interfaces such as `IMongoDocument`, `MongoIndexProvider`, `MongoCollectionAttribute`.
There's also a few extension methods of the MongoDB C# driver classes and interfaces that adds `IAsyncEnumerable` support to cursors.

Install the package `GSoft.Extensions.Mongo` at the application entry point level to register and configure the dependencies in a `IServiceCollection`.

Install the package `GSoft.Extensions.Mongo.Ephemeral` whenever you want to use a real but ephemeral MongoDB cluster with a single node replica set.
This is ideal for integration testing, as each `IServiceProvider` will have access to an unique and isolated database.


## Example

```csharp
// In the project that contains the documents:
// 1) Declare the collection name (camelCase) and the type responsible for providing indexes (optional)
[MongoCollection("people", IndexProviderType = typeof(PersonDocumentIndexes))]
public class PersonDocument : IMongoDocument
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
services
    .AddMongo(ConfigureDefaultMongoClient) // <-- register the default MongoDB client and database
    .AddNamedClient("anotherClient", ConfigureAnotherMongoClient) // <-- (optional) register multiple MongoDB clients with different options and connection strings
    .AddEncryptor<YourMongoValueEncryptor>() // (optional) <-- specify how to encrypt sensitive fields
    .ConfigureStaticOptions(ConfigureMongoStatic); // (optional) <-- specify MongoDB C# driver static settings

private static void ConfigureDefaultMongoClient(MongoClientOptions options)
{
    // Simplified for demo purposes, it is better to use appsettings.json, secret vaults
    // and IConfigureOptions<> classes that can use dependency injection to access other options or dependencies
    options.ConnectionString = "mongodb://localhost";
    options.DefaultDatabaseName = "default";
    options.EnableSensitiveInformationLogging = true;

    // Used by the automatic index update feature
    options.Indexing.LockMaxLifetimeInSeconds = 300;
    options.Indexing.LockAcquisitionTimeoutInSeconds = 60;

    // Modify MongoClientSettings (optional)
    options.MongoClientSettingsConfigurator = settings => { };
    
    // EXPERIMENTAL, FOR LOCAL DEVELOPMENT ONLY:
    // This will output a warning log when a collection scan is detected on a "find" command
    options.CommandPerformanceAnalysis.EnableCollectionScanDetection = true;
}

private static void ConfigureAnotherMongoClient(MongoClientOptions options)
{
    // Define options relative to this non-default MongoDB client
    // Ideally, use IConfigureNamedOptions<MongoClientOptions>
}

private static void ConfigureMongoStatic(MongoStaticOptions options)
{    
    // There are built-in serializers and conventions registered, but you can remove or override them
    // ⚠ Careful, these are objects that will live for the entire lifetime of the application (singleton) as MongoDB C# driver
    // uses static properties to configure its behavior and serialization
    options.BsonSerializers[typeof(Foo)] = new MyFooBsonSerializer();
    options.ConventionPacks.Add(new MyConventionPack());
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
// UpdateIndexesAsync() also accepts an optional registered MongoDB client name, database name and/or cancellation token. 
var indexer = this.Services.GetRequiredService<IMongoIndexer>();
await indexer.UpdateIndexesAsync(new[] { typeof(PersonDocument) });
await indexer.UpdateIndexesAsync(new[] { typeof(PersonDocument).Assembly }); // Assembly scanning alternative

// No need to know the collection name, just use the document type which must be decorated with MongoCollectionAttribute
var collection = this.Services.GetRequiredService<IMongoCollection<PersonDocument>>();
// OR: var collection = this.Services.GetRequiredService<IMongoDatabase>().GetCollection<PersonDocument>();

// No cursors handling needed, use IAsyncEnumerable
var people = await collection.Find(FilterDefinition<PersonDocument>.Empty).ToAsyncEnumerable();

// Access other registered MongoDB clients
var anotherMongoClient = this.Services.GetRequiredService<IMongoClientProvider>().GetClient("anotherClient");
```

```csharp
// 4) Add the GSoft.Extensions.Mongo.Ephemeral package to use a ephemeral but real MongoDB database in your tests
var services = new ServiceCollection();
services.AddMongo().UseEphemeralRealServer();
```


## License

Copyright © 2023, GSoft Group Inc. This code is licensed under the Apache License, Version 2.0. You may obtain a copy of this license at https://github.com/gsoft-inc/gsoft-license/blob/master/LICENSE.