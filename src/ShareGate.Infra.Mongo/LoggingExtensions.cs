using Microsoft.Extensions.Logging;

namespace ShareGate.Infra.Mongo;

// High-performance logging to prevent too many allocations
// https://docs.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator
internal static partial class LoggingExtensions
{
    // IndexProcessor
    [LoggerMessage(1, LogLevel.Information, "Skipping {DocumentType} index {IndexName} as it is already up-to-date")]
    public static partial void SkippingUpToDateIndex(this ILogger logger, string documentType, string indexName);

    [LoggerMessage(2, LogLevel.Information, "Dropping {DocumentType} index {IndexName} as its definition has changed")]
    public static partial void DroppingOutdatedIndex(this ILogger logger, string documentType, string indexName);

    [LoggerMessage(3, LogLevel.Information, "Dropping {DocumentType} index {IndexName} as it is not referenced in the code anymore")]
    public static partial void DroppingOrphanedIndex(this ILogger logger, string documentType, string indexName);

    [LoggerMessage(4, LogLevel.Information, "Creating {DocumentType} index {IndexName} for the first time")]
    public static partial void CreatingCompletelyNewIndex(this ILogger logger, string documentType, string indexName);

    [LoggerMessage(5, LogLevel.Information, "Creating {DocumentType} index {IndexName} after dropping an older version")]
    public static partial void CreatingUpdatedIndex(this ILogger logger, string documentType, string indexName);

    // MongoLoggingEventSubscriber
    [LoggerMessage(6, LogLevel.Debug, "Executing mongo command {MongoCommandName}:{MongoRequestId} {MongoCommandJson}")]
    public static partial void CommandStartedSensitive(this ILogger logger, string mongoCommandName, int mongoRequestId, string mongoCommandJson);

    [LoggerMessage(7, LogLevel.Debug, "Executing mongo command {MongoCommandName}:{MongoRequestId}")]
    public static partial void CommandStartedNonSensitive(this ILogger logger, string mongoCommandName, int mongoRequestId);

    [LoggerMessage(8, LogLevel.Debug, "Successfully executed mongo command {MongoCommandName}:{MongoRequestId} in {MongoCommandDuration} seconds")]
    public static partial void CommandSucceeded(this ILogger logger, string mongoCommandName, int mongoRequestId, double mongoCommandDuration);

    [LoggerMessage(EventId = 9, Message = "Failed to execute mongo command {MongoCommandName}:{MongoRequestId} in {MongoCommandDuration} seconds")]
    public static partial void CommandFailed(this ILogger logger, LogLevel logLevel, Exception? ex, string mongoCommandName, int mongoRequestId, double mongoCommandDuration);

    // MongoDistributedLock
    [LoggerMessage(10, LogLevel.Debug, "Attempting to acquire distributed lock {LockId} by owner {OwnerId}")]
    public static partial void AcquiringDistributedLock(this ILogger logger, string lockId, string ownerId);

    [LoggerMessage(11, LogLevel.Information, "Distributed lock {LockId} has been acquired by owner {OwnerId}")]
    public static partial void DistributedLockAcquired(this ILogger logger, string lockId, string ownerId);

    [LoggerMessage(12, LogLevel.Debug, "Distributed lock {LockId} has not been acquired by owner {OwnerId}")]
    public static partial void DistributedLockNotAcquired(this ILogger logger, string lockId, string ownerId);

    [LoggerMessage(13, LogLevel.Information, "Distributed lock {LockId} has been released by owner {OwnerId}")]
    public static partial void DistributedLockReleased(this ILogger logger, string lockId, string ownerId);
}