using MongoDB.Bson;

namespace ShareGate.Infra.Mongo.Performance;

internal readonly struct CommandToAnalyze
{
    public CommandToAnalyze(string commandName, BsonDocument command, int requestId)
    {
        this.CommandName = commandName;
        this.Command = (BsonDocument)command.DeepClone();
        this.RequestId = requestId;
    }

    public string CommandName { get; }

    public BsonDocument Command { get; }

    public int RequestId { get; }
}