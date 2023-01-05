using System.ComponentModel.DataAnnotations;
using MongoDB.Driver;

namespace GSoft.Extensions.Mongo;

public sealed class MongoClientOptions
{
    public const string SectionName = "Mongo";

    public MongoClientOptions()
    {
        this.Indexing = new MongoIndexingOptions();
        this.CommandPerformanceAnalysis = new MongoCommandPerformanceAnalysisOptions();
    }

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string DefaultDatabaseName { get; set; } = string.Empty;

    public bool EnableSensitiveInformationLogging { get; set; }

    public Action<MongoClientSettings>? MongoClientSettingsConfigurator { get; set; }

    public MongoIndexingOptions Indexing { get; }

    public MongoCommandPerformanceAnalysisOptions CommandPerformanceAnalysis { get; }
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

public sealed class MongoCommandPerformanceAnalysisOptions
{
    public bool EnableCollectionScanDetection { get; set; }

    internal bool IsPerformanceAnalysisEnabled
    {
        get => this.EnableCollectionScanDetection;
    }
}