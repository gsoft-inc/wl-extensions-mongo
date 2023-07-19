namespace Workleap.Extensions.Mongo.Performance;

internal static class CommandPerformanceConstants
{
    public static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedCommandsAndNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
    {
        // https://www.mongodb.com/docs/manual/reference/command/find/
        ["find"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "find", "filter", "sort", "projection", "hint", "skip", "limit", "batchSize", "singleBatch",
            "comment", "maxTimeMS", "readConcern", "max", "min", "returnKey", "showRecordId", "tailable",
            "oplogReplay", "noCursorTimeout", "awaitData", "allowPartialResults", "collation", "allowDiskUse", "let",
        },
    };
}