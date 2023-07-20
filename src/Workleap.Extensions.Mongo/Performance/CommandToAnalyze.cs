using MongoDB.Bson;

namespace Workleap.Extensions.Mongo.Performance;

internal readonly struct CommandToAnalyze
{
    public CommandToAnalyze(string databaseName, int requestId, string commandName, BsonDocument command)
    {
        this.DatabaseName = databaseName;
        this.CommandName = commandName;
        this.RequestId = requestId;

        // The original command could be a derived class of BsonDocument of the Mongo C# driver that could:
        // - Only be used once (throws if we use it twice)
        // - Implement IDisposable and hold things like byte array buffers
        // Because of that, we must deep clone the document
        this.Command = (BsonDocument)command.DeepClone();
    }

    public string DatabaseName { get; }

    public int RequestId { get; }

    public string CommandName { get; }

    public BsonDocument Command { get; }
}