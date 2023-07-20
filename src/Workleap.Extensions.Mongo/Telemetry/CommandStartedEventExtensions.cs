using MongoDB.Driver.Core.Events;

namespace Workleap.Extensions.Mongo.Telemetry;

internal static class CommandStartedEventExtensions
{
    private static readonly HashSet<string> CommandsWithCollectionNameAsValue = new HashSet<string>(StringComparer.Ordinal)
    {
        "aggregate",
        "count",
        "distinct",
        "mapReduce",
        "geoSearch",
        "delete",
        "find",
        "killCursors",
        "findAndModify",
        "insert",
        "update",
        "create",
        "drop",
        "createIndexes",
        "listIndexes",
    };

    public static string? GetCollectionName(this CommandStartedEvent evt)
    {
        if (evt.CommandName == "getMore")
        {
            if (evt.Command.Contains("collection"))
            {
                var collectionValue = evt.Command.GetValue("collection");
                if (collectionValue.IsString)
                {
                    return collectionValue.AsString;
                }
            }
        }
        else if (CommandsWithCollectionNameAsValue.Contains(evt.CommandName))
        {
            var commandValue = evt.Command.GetValue(evt.CommandName);
            if (commandValue is { IsString: true })
            {
                return commandValue.AsString;
            }
        }

        return null;
    }
}