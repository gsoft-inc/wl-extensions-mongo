namespace Workleap.Extensions.Mongo.Analyzers.Internals;

internal static class KnownSymbolNames
{
    public const string MongoAssembly = "MongoDB.Driver";
    public const string MongoCollectionExtensions = "MongoDB.Driver.IMongoCollectionExtensions";
    public const string MongoCollectionInterface = "MongoDB.Driver.IMongoCollection`1";

    public const string WorkleapMongoAssembly = "Workleap.Extensions.Mongo.Abstractions";
    public const string IndexedByAttribute = "Workleap.Extensions.Mongo.IndexedByAttribute";
    public const string NoIndexNeededAttribute = "Workleap.Extensions.Mongo.NoIndexNeededAttribute";
}