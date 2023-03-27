namespace GSoft.Extensions.Mongo;

[AttributeUsage(AttributeTargets.Class)]
public sealed class IndexedByAttribute : Attribute
{
    public IndexedByAttribute(params string[] indexes)
    {
        this.Indexes = indexes.Length > 0 ? indexes : throw new ArgumentException(nameof(indexes) + " cannot be empty");
    }

    public string[] Indexes { get; }
}