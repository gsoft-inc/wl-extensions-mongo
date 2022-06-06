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
    public const int NamePrefixMaxLength = 62;
    private const int NameSuffixLength = 64;

    // MongoDB 4.2 removed the index name length limit of 127 characters, but we still use this limit anyway, it should be enough:
    // https://www.mongodb.com/docs/manual/reference/limits/#mongodb-limit-Index-Name-Length
    private static readonly Regex ValidNameRegex = new Regex(
        "^(?<NamePrefix>[a-z0-9_]{0," + NamePrefixMaxLength + "})_(?<NameSuffix>[a-z0-9]{" + NameSuffixLength + "})$",
        RegexOptions.Compiled);

    private UniqueIndexName()
    {
    }

    public string Name { get; private init; } = string.Empty;

    public string NamePrefix { get; private init; } = string.Empty;

    public string NameSuffix { get; private init; } = string.Empty;

    public static bool TryCreate<TDocument>(CreateIndexModel<TDocument> indexModel, [MaybeNullWhen(false)] out UniqueIndexName indexName)
    {
        var options = indexModel.Options;

        var namePrefix = options.Name?.Trim() ?? string.Empty;
        var bsonSerializer = BsonSerializer.LookupSerializer<TDocument>();
        var serializerRegistry = BsonSerializer.SerializerRegistry;

        var bsonIndexFields = indexModel.Keys.Render(bsonSerializer, serializerRegistry).ToString();
        var indexDescription = new StringBuilder(bsonIndexFields)
            .Append(options.Unique.HasValue && options.Unique.Value ? "unique" : string.Empty)
            .Append(options.Sparse.HasValue && options.Sparse.Value ? "sparse" : string.Empty)
            .Append(options.WildcardProjection is { } projection ? projection.Render(bsonSerializer, serializerRegistry) : string.Empty)
            .Append(options.PartialFilterExpression is { } filter ? filter.Render(bsonSerializer, serializerRegistry) : string.Empty)
            .ToString();

        string nameSuffix;
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(indexDescription));
#if NET6_0_OR_GREATER
            nameSuffix = Convert.ToHexString(hash);
#else
            nameSuffix = BitConverter.ToString(hash).Replace("-", string.Empty);
#endif
        }

        var name = namePrefix + "_" + nameSuffix.ToLowerInvariant();

        if (ValidNameRegex.Match(name) is { Success: true })
        {
            indexName = new UniqueIndexName
            {
                Name = name,
                NamePrefix = namePrefix,
                NameSuffix = nameSuffix,
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
                Name = name,
                NamePrefix = match.Groups["NamePrefix"].Value,
                NameSuffix = match.Groups["NameSuffix"].Value,
            };

            return true;
        }

        indexName = null;
        return false;
    }
}