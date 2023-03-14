namespace GSoft.Extensions.Mongo;

[AttributeUsage(AttributeTargets.Class)]
public sealed class IndexedByAttribute : Attribute
{
    public IndexedByAttribute(params string[] indexes)
    {
        this.Indexes = indexes;
    }

    public string[] Indexes { get; }
}