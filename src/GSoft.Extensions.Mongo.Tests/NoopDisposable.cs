namespace GSoft.Extensions.Mongo.Tests;

internal sealed class NoopDisposable : IDisposable
{
    public static readonly IDisposable Instance = new NoopDisposable();

    private NoopDisposable()
    {
    }

    public void Dispose()
    {
    }
}