using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MongoDB.Bson.Serialization;

namespace GSoft.Infra.Mongo;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public MongoOptions()
    {
        this.BsonSerializers = new Dictionary<Type, IBsonSerializer>();
        this.ConventionPacks = new List<NamedConventionPack>();
    }

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string DefaultDatabaseName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? MinConnectionPoolSize { get; set; }

    [Range(1, int.MaxValue)]
    public int? MaxConnectionPoolSize { get; set; }

    public bool EnableSensitiveInformationLogging { get; set; }

    public IDictionary<Type, IBsonSerializer> BsonSerializers { get; }

    public IList<NamedConventionPack> ConventionPacks { get; }

    public MongoIndexingOptions Indexing { get; } = new MongoIndexingOptions();
}

public sealed class MongoIndexingOptions
{
    [Required]
    public string DistributedLockName { get; set; } = "mongo-indexing";

    [Required]
    [Range(1, int.MaxValue)]
    public int LockMaxLifetimeInSeconds { get; set; } = 300;

    [Required]
    [Range(1, int.MaxValue)]
    public int LockAcquisitionTimeoutInSeconds { get; set; } = 60;
}