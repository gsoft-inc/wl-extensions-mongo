namespace Workleap.Extensions.Mongo;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IndexedByAttribute : Attribute
{
    public IndexedByAttribute(params string[] indexes)
    {
        this.Indexes = indexes.Length > 0 ? indexes : throw new ArgumentException(nameof(indexes) + " cannot be empty");
    }

    public string[] Indexes { get; }
}