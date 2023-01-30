using System.ComponentModel.DataAnnotations;
using MongoDB.Driver;

namespace GSoft.Extensions.Mongo;

public sealed class MongoClientOptions
{
    public const string SectionName = "Mongo";

    public MongoClientOptions()
    {
        this.Indexing = new MongoIndexingOptions();
        this.Telemetry = new MongoTelemetryOptions();
        this.CommandPerformanceAnalysis = new MongoCommandPerformanceAnalysisOptions();
    }

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string DefaultDatabaseName { get; set; } = string.Empty;

    public Action<MongoClientSettings>? MongoClientSettingsConfigurator { get; set; }

    public MongoIndexingOptions Indexing { get; }

    public MongoTelemetryOptions Telemetry { get; }

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

public sealed class MongoTelemetryOptions
{
    public MongoTelemetryOptions()
    {
        this.IgnoredCommandNames = new HashSet<string>(StringComparer.Ordinal)
        {
            // Inspired from Officevibe's ignored command names list
            "isMaster", "buildInfo", "saslStart", "saslContinue", "getLastError", "getMore", "listIndexes", "ping",
        };
    }

    public bool CaptureCommandText { get; set; }

    public ISet<string> IgnoredCommandNames { get; }
}

public sealed class MongoCommandPerformanceAnalysisOptions
{
    public bool EnableCollectionScanDetection { get; set; }

    internal bool IsPerformanceAnalysisEnabled
    {
        get => this.EnableCollectionScanDetection;
    }
}