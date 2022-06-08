using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GSoft.Infra.Mongo.Indexing;

internal sealed class UniqueIndexNameFactory : IUniqueIndexNameFactory
{
    private readonly Version _applicationVersion;

    public UniqueIndexNameFactory(IOptions<MongoOptions> options, IHostEnvironment environment)
    {
        var applicationVersion = options.Value.Indexing.ApplicationVersion ?? UniqueIndexName.DefaultVersion;

        // Make negative properties equal to zero
        var sanitizedApplicationVersion = new Version(
            applicationVersion.Major >= 0 ? applicationVersion.Major : UniqueIndexName.DefaultVersion.Major,
            applicationVersion.Minor >= 0 ? applicationVersion.Minor : UniqueIndexName.DefaultVersion.Minor,
            applicationVersion.Build >= 0 ? applicationVersion.Build : UniqueIndexName.DefaultVersion.Build,
            applicationVersion.Revision >= 0 ? applicationVersion.Revision : UniqueIndexName.DefaultVersion.Revision);

        if (this._applicationVersion == UniqueIndexName.DefaultVersion && (environment.IsStaging() || environment.IsProduction()))
        {
            throw new InvalidOperationException(nameof(MongoOptions) + "." + nameof(MongoOptions.Indexing) + "." + nameof(MongoOptions.Indexing.ApplicationVersion) + " must return a positive application version");
        }

        this._applicationVersion = sanitizedApplicationVersion;
    }

    public bool TryCreate<TDocument>(CreateIndexModel<TDocument> indexModel, [MaybeNullWhen(false)] out UniqueIndexName indexName)
    {
        return UniqueIndexName.TryCreate(indexModel, this._applicationVersion, out indexName);
    }

    public bool TryCreate(BsonValue indexDocument, [MaybeNullWhen(false)] out UniqueIndexName indexName)
    {
        return UniqueIndexName.TryCreate(indexDocument, out indexName);
    }
}