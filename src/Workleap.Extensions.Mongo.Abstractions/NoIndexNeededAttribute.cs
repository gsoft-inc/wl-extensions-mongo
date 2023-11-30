namespace Workleap.Extensions.Mongo;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoIndexNeededAttribute : Attribute
{
    public NoIndexNeededAttribute(string reason)
    {
        this.Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    public string Reason { get; }
}