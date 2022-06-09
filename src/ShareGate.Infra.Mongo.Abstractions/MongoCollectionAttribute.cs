using System.Text.RegularExpressions;

namespace ShareGate.Infra.Mongo;

[AttributeUsage(AttributeTargets.Class)]
public sealed class MongoCollectionAttribute : Attribute
{
    private static readonly Regex ValidNameRegex = new Regex("^[a-z][a-zA-Z0-9]{0,63}$", RegexOptions.Compiled);

    public MongoCollectionAttribute(string name)
    {
        this.Name = ValidNameRegex.IsMatch(name) ? name : throw new ArgumentException("Collection name must match the regex " + ValidNameRegex, nameof(name));
    }

    public string Name { get; }

    public Type? IndexProviderType { get; init; }
}