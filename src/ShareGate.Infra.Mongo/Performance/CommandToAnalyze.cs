using MongoDB.Bson;

namespace ShareGate.Infra.Mongo.Performance;

internal readonly struct CommandToAnalyze
{
    public CommandToAnalyze(string databaseName, int requestId, string commandName, BsonDocument command)
    {
        this.DatabaseName = databaseName;
        this.CommandName = commandName;
        this.RequestId = requestId;

        // The original command might be a RawBsonDocument which is not reusable, so we need to clone it
        this.Command = (BsonDocument)command.DeepClone();
    }

    public string DatabaseName { get; }

    public int RequestId { get; }

    public string CommandName { get; }

    public BsonDocument Command { get; }
}