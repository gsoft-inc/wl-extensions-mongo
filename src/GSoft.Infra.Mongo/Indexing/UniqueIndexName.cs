using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace GSoft.Infra.Mongo.Indexing;

/// <summary>
/// Represents a unique index name generated from an instance of CreateIndexModel&lt;TDocument&gt;.
/// The index name is composed of the developer-entered index name concatenated to the SHA256 hash of the index definition.
/// Any change to the index definition (new fields, uniqueness, etc.) will change the hash, and that's how we can know if an index should be updated.
/// </summary>
internal sealed class UniqueIndexName
{
    private const int IndexHashLength = 64;

    public static readonly Version DefaultVersion = new Version(0, 0, 0, 0);

    // Careful, pre-4.2 MongoDB has a index name length limit of 127 characters
    // https://www.mongodb.com/docs/manual/reference/limits/#mongodb-limit-Index-Name-Length
    private static readonly Regex ValidNameRegex = new Regex(
        "^(?<Prefix>[a-z0-9_]+)_(?<Hash>[a-z0-9]{" + IndexHashLength + "})_(?<AppVersion>[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+)$",
        RegexOptions.Compiled);

    private UniqueIndexName()
    {
    }

    public string FullName { get; private init; } = string.Empty;

    public string Prefix { get; private init; } = string.Empty;

    public string Hash { get; private init; } = string.Empty;

    public Version ApplicationVersion { get; private init; } = DefaultVersion;

    public static bool TryCreate<TDocument>(CreateIndexModel<TDocument> indexModel, Version applicationVersion, [MaybeNullWhen(false)] out UniqueIndexName indexName)
    {
        var options = indexModel.Options;

        var prefix = options.Name?.Trim() ?? string.Empty;
        if (prefix.Length == 0)
        {
            indexName = null;
            return false;
        }

        var bsonSerializer = BsonSerializer.LookupSerializer<TDocument>();
        var serializerRegistry = BsonSerializer.SerializerRegistry;

        var bsonIndexFields = indexModel.Keys.Render(bsonSerializer, serializerRegistry).ToString();
        var indexDescription = new StringBuilder(bsonIndexFields)
            .Append(options.Unique.HasValue && options.Unique.Value ? "unique" : string.Empty)
            .Append(options.Sparse.HasValue && options.Sparse.Value ? "sparse" : string.Empty)
            .Append(options.WildcardProjection is { } projection ? projection.Render(bsonSerializer, serializerRegistry) : string.Empty)
            .Append(options.PartialFilterExpression is { } filter ? filter.Render(bsonSerializer, serializerRegistry) : string.Empty)
            .ToString();

        string hashHex;
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(indexDescription));
#if NET6_0_OR_GREATER
            hashHex = Convert.ToHexString(hash).ToLowerInvariant();
#else
            hashHex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
#endif
        }

        var name = prefix + "_" + hashHex + "_" + applicationVersion;

        if (ValidNameRegex.Match(name) is { Success: true })
        {
            indexName = new UniqueIndexName
            {
                FullName = name,
                Prefix = prefix,
                Hash = hashHex,
                ApplicationVersion = applicationVersion,
            };

            return true;
        }

        indexName = null;
        return false;
    }

    public static bool TryCreate(BsonValue indexDocument, [MaybeNullWhen(false)] out UniqueIndexName indexName)
    {
        var name = indexDocument["name"]?.AsString ?? string.Empty;

        if (ValidNameRegex.Match(name) is { Success: true } match)
        {
            indexName = new UniqueIndexName
            {
                FullName = name,
                Prefix = match.Groups["Prefix"].Value,
                Hash = match.Groups["Hash"].Value,
                ApplicationVersion = Version.Parse(match.Groups["AppVersion"].Value),
            };

            return true;
        }

        indexName = null;
        return false;
    }
}